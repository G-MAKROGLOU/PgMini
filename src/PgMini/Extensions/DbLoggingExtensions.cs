using Microsoft.Extensions.Logging;
using PgMini.Models;

namespace PgMini.Extensions;

internal static class DbLoggingExtensions
{
    private static string ParamsToString(this List<QueryParams>? parameters) =>
        parameters is null
            ? "(none)"
            : string.Join(" | ", parameters.Select(p => $"@{p.Key}={p.Value}"));

    internal static void LogQueryError(this ILogger logger, string message, string query, List<QueryParams>? parameters) =>
        logger.LogError(
            "DB Error: {Error} | Query: {Query} | Params: {Params}",
            message, query, parameters.ParamsToString());

    internal static void LogReadQuery(this ILogger logger, string query, int count, List<QueryParams>? parameters) =>
        logger.LogDebug(
            "DB Read: {Count} row(s) | Query: {Query} | Params: {Params}",
            count, query, parameters.ParamsToString());

    internal static void LogExecuteQuery(this ILogger logger, string query, int affected, List<QueryParams>? parameters) =>
        logger.LogDebug(
            "DB Execute: {Affected} row(s) affected | Query: {Query} | Params: {Params}",
            affected, query, parameters.ParamsToString());

    internal static void LogExecuteScalarQuery<T>(this ILogger logger, string query, T? returned, List<QueryParams>? parameters) =>
        logger.LogDebug(
            "DB Scalar: {Returned} | Query: {Query} | Params: {Params}",
            returned, query, parameters.ParamsToString());

    internal static void LogColumnMappingWarning(this ILogger logger, string columnName) =>
        logger.LogWarning(
            "PgMini: No property mapped to column '{Column}'. Add [Column(\"{Column}\")] to your model or remove the column from SELECT.",
            columnName, columnName);

    internal static void LogTransactionalError(this ILogger logger, string message) =>
        logger.LogError(
            "Transaction failed — all changes rolled back. Error: {Error}", message);
}
