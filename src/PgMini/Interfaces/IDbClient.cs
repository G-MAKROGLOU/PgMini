using System.Data;
using Npgsql;
using PgMini.Delegates;
using PgMini.Models;

namespace PgMini.Interfaces;

/// <summary>
/// PostgreSQL database client. Provides typed query execution, scalar reads, batch operations,
/// and a composable transactional pipeline.
/// </summary>
public interface IDbClient
{
    // ── Reads ──────────────────────────────────────────────────────────────────

    /// <summary>Executes <paramref name="query"/> and maps all rows to <typeparamref name="T"/>.</summary>
    Task<List<T>> ReadAsync<T>(
        string query,
        List<QueryParams>? parameters = null,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default
    ) where T : new();

    /// <summary>Returns the first matching row, or <c>null</c> if the result set is empty.</summary>
    Task<T?> ReadFirstOrDefaultAsync<T>(
        string query,
        List<QueryParams>? parameters = null,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default
    ) where T : new();

    /// <summary>
    /// Returns exactly one row. Throws <see cref="InvalidOperationException"/> if the result
    /// set is empty or contains more than one row.
    /// </summary>
    Task<T> ReadSingleAsync<T>(
        string query,
        List<QueryParams>? parameters = null,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default
    ) where T : new();

    /// <summary>
    /// Executes <paramref name="query"/> and returns one page of results.
    /// Appends <c>LIMIT</c> and <c>OFFSET</c> automatically — do NOT include them in your query.
    /// </summary>
    Task<List<T>> ReadPagedAsync<T>(
        string query,
        int pageSize,
        int offset = 0,
        List<QueryParams>? parameters = null,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default
    ) where T : new();

    /// <summary>
    /// Streams rows from <paramref name="query"/> as an <see cref="IAsyncEnumerable{T}"/>,
    /// yielding each row as it arrives without buffering the full result set in memory.
    /// Ideal for large result sets or when processing rows one-by-one is sufficient.
    /// </summary>
    IAsyncEnumerable<T> StreamAsync<T>(
        string query,
        List<QueryParams>? parameters = null,
        CancellationToken cancellationToken = default
    ) where T : new();

    // ── Exists ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes a <c>SELECT EXISTS(...)</c> query and returns the boolean result.
    /// </summary>
    Task<bool> ExistsAsync(
        string query,
        List<QueryParams>? parameters = null,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default
    );

    // ── Writes ─────────────────────────────────────────────────────────────────

    /// <summary>Executes a non-query command and returns the number of affected rows.</summary>
    Task<int> ExecuteAsync(
        string query,
        List<QueryParams>? parameters = null,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Executes multiple commands in a single round-trip using <c>NpgsqlBatch</c>.
    /// Returns the total number of affected rows.
    /// </summary>
    Task<int> ExecuteBatchAsync(
        IEnumerable<BatchCommand> commands,
        CancellationToken cancellationToken = default
    );

    /// <summary>Executes a scalar query and returns the single value, or <c>null</c>.</summary>
    Task<T?> ExecuteScalarAsync<T>(
        string query,
        List<QueryParams>? parameters = null,
        NpgsqlTransaction? transaction = null,
        CancellationToken cancellationToken = default
    );

    // ── Transactional pipeline ─────────────────────────────────────────────────

    /// <summary>
    /// Runs all steps in <paramref name="tx"/> inside a single PostgreSQL transaction at the
    /// specified <paramref name="isolationLevel"/> (default: <see cref="IsolationLevel.ReadCommitted"/>).
    /// Commits on success; rolls back and rethrows on any exception.
    /// Returns the result of the last step, cast to <typeparamref name="T"/>.
    /// </summary>
    Task<T?> RunTransactional<T>(
        Transaction tx,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default
    );

    // ── Step builder helpers ───────────────────────────────────────────────────

    /// <summary>Creates a READ step that uses the query and params forwarded from the previous step.</summary>
    TxStep TxReadStep<T>() where T : new();

    /// <summary>Creates a READ step with a fixed query and params, ignoring forwarded values.</summary>
    TxStep AutonomousTxReadStep<T>(string query, List<QueryParams>? parameters = null) where T : new();

    /// <summary>Creates an EXECUTE step that uses the query and params forwarded from the previous step.</summary>
    TxStep TxExecuteStep();

    /// <summary>Creates an EXECUTE step with a fixed query and params, ignoring forwarded values.</summary>
    TxStep AutonomousTxExecuteStep(string query, List<QueryParams>? parameters = null);

    /// <summary>Creates a SCALAR step that uses the query and params forwarded from the previous step.</summary>
    TxStep TxExecuteScalarStep<T>();

    /// <summary>Creates a SCALAR step with a fixed query and params, ignoring forwarded values.</summary>
    TxStep AutonomousTxExecuteScalarStep<T>(string query, List<QueryParams>? parameters = null);
}
