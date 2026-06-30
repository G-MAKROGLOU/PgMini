using Microsoft.Extensions.Logging;
using PgMini.Models;

namespace PgMini.Extensions;

// All high-frequency log calls route through [LoggerMessage] source-generated stubs for
// zero-allocation at disabled log levels (no boxing, no delegate captures).
internal static partial class DbLoggingExtensions
{
    // ── Public wrappers (called by DbClient) ──────────────────────────────────

    internal static void LogQueryError(this ILogger logger, string message, string query, List<QueryParams>? parameters)
    {
        if (logger.IsEnabled(LogLevel.Error))
            LogQueryErrorCore(logger, message, query, parameters.ParamsToString());
    }

    internal static void LogReadQuery(this ILogger logger, string query, int count, List<QueryParams>? parameters)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            LogReadQueryCore(logger, count, query, parameters.ParamsToString());
    }

    internal static void LogExecuteQuery(this ILogger logger, string query, int affected, List<QueryParams>? parameters)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            LogExecuteQueryCore(logger, affected, query, parameters.ParamsToString());
    }

    internal static void LogExecuteScalarQuery<T>(this ILogger logger, string query, T? returned, List<QueryParams>? parameters)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            LogExecuteScalarQueryCore(logger, returned?.ToString(), query, parameters.ParamsToString());
    }

    internal static void LogColumnMappingWarning(this ILogger logger, string columnName) =>
        LogColumnMappingWarningCore(logger, columnName);

    internal static void LogTransactionalError(this ILogger logger, string message) =>
        LogTransactionalErrorCore(logger, message);

    internal static void LogBatchError(this ILogger logger, string message) =>
        LogBatchErrorCore(logger, message);

    internal static void LogTransactionStart(this ILogger logger, string name) =>
        LogTransactionStartCore(logger, name);

    internal static void LogTransactionCommitted(this ILogger logger, string name) =>
        LogTransactionCommittedCore(logger, name);

    internal static void LogTransactionEnd(this ILogger logger, string name) =>
        LogTransactionEndCore(logger, name);

    // ── [LoggerMessage] source-generated stubs ─────────────────────────────────

    [LoggerMessage(Level = LogLevel.Error, Message = "DB Error: {Error} | Query: {Query} | Params: {Params}")]
    private static partial void LogQueryErrorCore(ILogger logger, string error, string query, string @params);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DB Read: {Count} row(s) | Query: {Query} | Params: {Params}")]
    private static partial void LogReadQueryCore(ILogger logger, int count, string query, string @params);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DB Execute: {Affected} row(s) affected | Query: {Query} | Params: {Params}")]
    private static partial void LogExecuteQueryCore(ILogger logger, int affected, string query, string @params);

    [LoggerMessage(Level = LogLevel.Debug, Message = "DB Scalar: {Returned} | Query: {Query} | Params: {Params}")]
    private static partial void LogExecuteScalarQueryCore(ILogger logger, string? returned, string query, string @params);

    [LoggerMessage(Level = LogLevel.Warning, Message = "PgMini: Column '{Column}' is not mapped — add a [Column] attribute or remove it from SELECT.")]
    private static partial void LogColumnMappingWarningCore(ILogger logger, string column);

    [LoggerMessage(Level = LogLevel.Error, Message = "Transaction failed — all changes rolled back. Error: {Error}")]
    private static partial void LogTransactionalErrorCore(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Batch execution failed: {Error}")]
    private static partial void LogBatchErrorCore(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "=== Transaction [{Name}] Start ===")]
    private static partial void LogTransactionStartCore(ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "=== Transaction [{Name}] Committed ===")]
    private static partial void LogTransactionCommittedCore(ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "=== Transaction [{Name}] End ===")]
    private static partial void LogTransactionEndCore(ILogger logger, string name);

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string ParamsToString(this List<QueryParams>? parameters) =>
        parameters is null
            ? "(none)"
            : string.Join(" | ", parameters.Select(p => $"@{p.Key}={p.Value}"));
}
