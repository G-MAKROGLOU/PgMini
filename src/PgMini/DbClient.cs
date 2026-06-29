using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Npgsql;
using PgMini.Delegates;
using PgMini.Extensions;
using PgMini.Interfaces;
using PgMini.Models;

namespace PgMini;

/// <inheritdoc />
public sealed class DbClient : IDbClient, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<DbClient> _logger;
    private readonly bool _ownsDataSource;

    // Per-type mapping cache — shared across all DbClient instances.
    // Entries are built once (on first query for a type) and reused on every subsequent call.
    private static readonly ConcurrentDictionary<Type, ColumnMapping[]> _mappingCache = new();

    // Open generic definition of DbDataReader.GetFieldValue<T>(int) — used to emit typed readers.
    private static readonly MethodInfo _getFieldValueDef =
        typeof(DbDataReader).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == nameof(DbDataReader.GetFieldValue) &&
                        m.IsGenericMethodDefinition &&
                        m.GetGenericArguments().Length == 1 &&
                        m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType == typeof(int));

    // Mapper — compiled once per property; calls the typed reader and sets the property directly,
    // eliminating both PropertyInfo.SetValue overhead and value-type boxing from GetValue().
    private sealed record ColumnMapping(
        PropertyInfo Property,
        string ColumnName,
        string? TypeName,
        Action<NpgsqlDataReader, int, object> Mapper
    );

    /// <summary>
    /// Initialises a client that shares an externally-owned <see cref="NpgsqlDataSource"/>.
    /// The data source is NOT disposed when this client is disposed — the caller manages its lifetime.
    /// </summary>
    public DbClient(NpgsqlDataSource dataSource, ILogger<DbClient> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
        _ownsDataSource = false;
    }

    /// <summary>
    /// Initialises a client that builds and owns its own <see cref="NpgsqlDataSource"/>.
    /// Dynamic JSON and out-of-order metadata are enabled by default.
    /// The data source is disposed when this client is disposed.
    /// </summary>
    public DbClient(string connectionString, ILogger<DbClient> logger)
    {
        _dataSource = new NpgsqlDataSourceBuilder(connectionString)
            .EnableDynamicJson()
            .Build();
        _logger = logger;
        _ownsDataSource = true;
    }

    // ── Reads ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<List<T>> ReadAsync<T>(
        string query,
        List<QueryParams>? parameters = null,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default
    ) where T : new()
    {
        if (transaction is not null)
        {
            var result = await ExecuteReadCoreAsync<T>(transaction.Connection!, query, parameters, transaction, int.MaxValue, cancellationToken);
            _logger.LogReadQuery(query, result.Count, parameters);
            return result;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await ExecuteReadCoreAsync<T>(connection, query, parameters, null, int.MaxValue, cancellationToken);
        _logger.LogReadQuery(query, rows.Count, parameters);
        return rows;
    }

    /// <inheritdoc />
    public async Task<T?> ReadFirstOrDefaultAsync<T>(
        string query,
        List<QueryParams>? parameters = null,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default
    ) where T : new()
    {
        if (transaction is not null)
        {
            var result = await ExecuteReadCoreAsync<T>(transaction.Connection!, query, parameters, transaction, 1, cancellationToken);
            return result.Count > 0 ? result[0] : default;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await ExecuteReadCoreAsync<T>(connection, query, parameters, null, 1, cancellationToken);
        return rows.Count > 0 ? rows[0] : default;
    }

    /// <inheritdoc />
    public async Task<T> ReadSingleAsync<T>(
        string query,
        List<QueryParams>? parameters = null,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default
    ) where T : new()
    {
        // Read up to 2 rows so we can detect the "more than one" case without scanning the full set.
        List<T> rows;
        if (transaction is not null)
        {
            rows = await ExecuteReadCoreAsync<T>(transaction.Connection!, query, parameters, transaction, 2, cancellationToken);
        }
        else
        {
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            rows = await ExecuteReadCoreAsync<T>(connection, query, parameters, null, 2, cancellationToken);
        }

        return rows.Count switch
        {
            0 => throw new InvalidOperationException($"ReadSingleAsync<{typeof(T).Name}>: query returned no rows."),
            > 1 => throw new InvalidOperationException($"ReadSingleAsync<{typeof(T).Name}>: query returned more than one row."),
            _ => rows[0]
        };
    }

    /// <inheritdoc />
    public Task<List<T>> ReadPagedAsync<T>(
        string query,
        int pageSize,
        int offset = 0,
        List<QueryParams>? parameters = null,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default
    ) where T : new()
    {
        // Append LIMIT/OFFSET using reserved internal parameter names that won't clash with caller params.
        var pagedQuery = $"{query.TrimEnd(';', ' ', '\n', '\r', '\t')} LIMIT @__pgmini_limit OFFSET @__pgmini_offset";
        var pagedParams = (parameters ?? [])
            .Concat([QueryParams.Of("__pgmini_limit", pageSize), QueryParams.Of("__pgmini_offset", offset)])
            .ToList();
        return ReadAsync<T>(pagedQuery, pagedParams, transaction, cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<T> StreamAsync<T>(
        string query,
        List<QueryParams>? parameters = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    ) where T : new()
    {
        var mappings = GetMappings(typeof(T));

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(query, connection);
        ApplyParameters(command, parameters);

        NpgsqlDataReader reader;
        try
        {
            reader = await command.ExecuteReaderAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogQueryError(ex.Message, query, parameters);
            throw;
        }

        await using (reader)
        {
            var columnOrdinals = BuildOrdinalMap(reader);
            var activeMappings = ResolveActiveMappings(mappings, columnOrdinals);

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new T();
                foreach (var (mapping, ordinal) in activeMappings)
                {
                    if (!reader.IsDBNull(ordinal))
                        mapping.Mapper(reader, ordinal, row);
                }
                yield return row;
            }
        }
    }

    // ── Exists ─────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(
        string query,
        List<QueryParams>? parameters = null,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default
    )
    {
        var result = await ExecuteScalarAsync<bool>(query, parameters, transaction, cancellationToken);
        return result is true;
    }

    // ── Writes ─────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(
        string query,
        List<QueryParams>? parameters = null,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (transaction is not null)
            {
                var affected = await ExecuteCommandCoreAsync(transaction.Connection!, query, parameters, transaction, cancellationToken);
                _logger.LogExecuteQuery(query, affected, parameters);
                return affected;
            }

            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            var rowsAffected = await ExecuteCommandCoreAsync(connection, query, parameters, null, cancellationToken);
            _logger.LogExecuteQuery(query, rowsAffected, parameters);
            return rowsAffected;
        }
        catch (Exception ex)
        {
            _logger.LogQueryError(ex.Message, query, parameters);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> ExecuteBatchAsync(
        IEnumerable<BatchCommand> commands,
        CancellationToken cancellationToken = default
    )
    {
        var commandList = commands.ToList();
        if (commandList.Count == 0) return 0;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var batch = new NpgsqlBatch(connection);

        foreach (var cmd in commandList)
        {
            var batchCmd = new NpgsqlBatchCommand(cmd.Query);
            if (cmd.Parameters is not null)
                foreach (var p in cmd.Parameters)
                    batchCmd.Parameters.Add(new NpgsqlParameter($"@{p.Key}", p.Value ?? DBNull.Value));
            batch.BatchCommands.Add(batchCmd);
        }

        try
        {
            return await batch.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogBatchError(ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<T?> ExecuteScalarAsync<T>(
        string query,
        List<QueryParams>? parameters = null,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            T? result;
            if (transaction is not null)
            {
                result = await ExecuteScalarCoreAsync<T>(transaction.Connection!, query, parameters, transaction, cancellationToken);
            }
            else
            {
                await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
                result = await ExecuteScalarCoreAsync<T>(connection, query, parameters, null, cancellationToken);
            }

            _logger.LogExecuteScalarQuery(query, result, parameters);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogQueryError(ex.Message, query, parameters);
            throw;
        }
    }

    // ── Transactional pipeline ─────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<T?> RunTransactional<T>(
        Transaction tx,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogTransactionStart(tx.TransactionGroupName);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken);

        try
        {
            string? nextQuery = null;
            List<QueryParams>? nextParams = null;
            object? result = null;

            foreach (var step in tx.Queries)
            {
                result = await step.Runnable(nextQuery, nextParams, transaction);

                (nextQuery, nextParams) = step switch
                {
                    { PostRead: not null } => await step.PostRead(result!),
                    { PostExecute: not null } => await step.PostExecute((int)result!),
                    { PostExecuteScalar: not null } => await step.PostExecuteScalar(result!),
                    _ => (null, null)
                };
            }

            await transaction.CommitAsync(cancellationToken);
            _logger.LogTransactionCommitted(tx.TransactionGroupName);
            return (T?)result;
        }
        catch (Exception ex)
        {
            _logger.LogTransactionalError(ex.Message);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            _logger.LogTransactionEnd(tx.TransactionGroupName);
        }
    }

    // ── Step builder helpers ───────────────────────────────────────────────────

    /// <inheritdoc />
    public TxStep TxReadStep<T>() where T : new() =>
        async (q, p, tx) => await ReadAsync<T>(q!, p, tx);

    /// <inheritdoc />
    public TxStep AutonomousTxReadStep<T>(string query, List<QueryParams>? parameters = null) where T : new() =>
        async (_, _, tx) => await ReadAsync<T>(query, parameters, tx);

    /// <inheritdoc />
    public TxStep TxExecuteStep() =>
        async (q, p, tx) => await ExecuteAsync(q!, p, tx);

    /// <inheritdoc />
    public TxStep AutonomousTxExecuteStep(string query, List<QueryParams>? parameters = null) =>
        async (_, _, tx) => await ExecuteAsync(query, parameters, tx);

    /// <inheritdoc />
    public TxStep TxExecuteScalarStep<T>() =>
        async (q, p, tx) => await ExecuteScalarAsync<T>(q!, p, tx);

    /// <inheritdoc />
    public TxStep AutonomousTxExecuteScalarStep<T>(string query, List<QueryParams>? parameters = null) =>
        async (_, _, tx) => await ExecuteScalarAsync<T>(query, parameters, tx);

    // ── IAsyncDisposable ───────────────────────────────────────────────────────

    /// <summary>Disposes the underlying <see cref="NpgsqlDataSource"/> if this client owns it.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_ownsDataSource)
            await _dataSource.DisposeAsync();
    }

    // ── Private implementation ─────────────────────────────────────────────────

    private async Task<List<T>> ExecuteReadCoreAsync<T>(
        NpgsqlConnection connection,
        string query,
        List<QueryParams>? parameters,
        NpgsqlTransaction? transaction,
        int maxRows,
        CancellationToken cancellationToken
    ) where T : new()
    {
        var resultSet = new List<T>();
        var mappings = GetMappings(typeof(T));

        try
        {
            await using var command = new NpgsqlCommand(query, connection, transaction);
            ApplyParameters(command, parameters);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // Build an ordinal map from actual result columns (once, outside the row loop).
            var columnOrdinals = BuildOrdinalMap(reader);
            var activeMappings = ResolveActiveMappings(mappings, columnOrdinals);

            while (await reader.ReadAsync(cancellationToken) && resultSet.Count < maxRows)
            {
                var row = new T();
                foreach (var (mapping, ordinal) in activeMappings)
                {
                    if (!reader.IsDBNull(ordinal))
                        mapping.Mapper(reader, ordinal, row);
                }
                resultSet.Add(row);
            }
        }
        catch (Exception ex)
        {
            _logger.LogQueryError(ex.Message, query, parameters);
            throw;
        }

        return resultSet;
    }

    private static async Task<int> ExecuteCommandCoreAsync(
        NpgsqlConnection connection,
        string query,
        List<QueryParams>? parameters,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken
    )
    {
        await using var command = new NpgsqlCommand(query, connection, transaction);
        ApplyParameters(command, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<T?> ExecuteScalarCoreAsync<T>(
        NpgsqlConnection connection,
        string query,
        List<QueryParams>? parameters,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken
    )
    {
        await using var command = new NpgsqlCommand(query, connection, transaction);
        ApplyParameters(command, parameters);
        var returned = await command.ExecuteScalarAsync(cancellationToken);
        return returned is null or DBNull ? default : (T)returned;
    }

    private static void ApplyParameters(NpgsqlCommand command, List<QueryParams>? parameters)
    {
        if (parameters is null) return;
        foreach (var p in parameters)
            command.Parameters.Add(new NpgsqlParameter($"@{p.Key}", p.Value ?? DBNull.Value));
    }

    private static ColumnMapping[] GetMappings(Type type) =>
        _mappingCache.GetOrAdd(type, static t =>
            t.GetProperties()
             .Select(p => (Property: p, Attr: p.GetCustomAttribute<ColumnAttribute>()))
             .Where(x => x.Attr?.Name is not null)
             .Select(x => new ColumnMapping(
                 x.Property,
                 x.Attr!.Name!,
                 x.Attr.TypeName,
                 BuildMapper(x.Property, x.Attr.TypeName)))
             .ToArray());

    // Compiles a delegate that reads the typed column value and assigns it directly to the property —
    // no SetValue() overhead, no GetValue() boxing. Falls back to reflection for exotic types.
    private static Action<NpgsqlDataReader, int, object> BuildMapper(PropertyInfo property, string? typeName)
    {
        try
        {
            var readerParam   = Expression.Parameter(typeof(NpgsqlDataReader), "r");
            var ordinalParam  = Expression.Parameter(typeof(int), "i");
            var instanceParam = Expression.Parameter(typeof(object), "obj");

            var propType = property.PropertyType;

            // jsonb/json: the driver returns JsonDocument regardless of the CLR property type.
            // Nullable value types (int?, bool?…): unwrap to T so GetFieldValue<T> can be called.
            var underlyingType = typeName is "jsonb" or "json"
                ? typeof(JsonDocument)
                : (Nullable.GetUnderlyingType(propType) ?? propType);

            // reader.GetFieldValue<UnderlyingType>(ordinal) — typed, no boxing
            Expression readExpr = Expression.Call(
                readerParam,
                _getFieldValueDef.MakeGenericMethod(underlyingType),
                ordinalParam);

            // Widen int → int? (or any T → T?) when the property is a nullable value type.
            if (propType.IsValueType && Nullable.GetUnderlyingType(propType) is not null)
                readExpr = Expression.Convert(readExpr, propType);

            var castInstance = Expression.Convert(instanceParam, property.DeclaringType!);
            var setExpr = Expression.Assign(Expression.Property(castInstance, property), readExpr);

            return Expression.Lambda<Action<NpgsqlDataReader, int, object>>(
                setExpr, readerParam, ordinalParam, instanceParam).Compile();
        }
        catch
        {
            // Fallback: reflection path for any type GetFieldValue<T> can't emit at this stage.
            return (reader, ordinal, instance) =>
                property.SetValue(instance, reader.GetValue(ordinal));
        }
    }

    private static Dictionary<string, int> BuildOrdinalMap(NpgsqlDataReader reader)
    {
        var map = new Dictionary<string, int>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
            map[reader.GetName(i)] = i;
        return map;
    }

    private (ColumnMapping Mapping, int Ordinal)[] ResolveActiveMappings(
        ColumnMapping[] mappings,
        Dictionary<string, int> columnOrdinals)
    {
        var active = new List<(ColumnMapping, int)>(mappings.Length);
        foreach (var m in mappings)
        {
            if (columnOrdinals.TryGetValue(m.ColumnName, out var ordinal))
                active.Add((m, ordinal));
            else
                _logger.LogColumnMappingWarning(m.ColumnName);
        }
        return active.ToArray();
    }
}
