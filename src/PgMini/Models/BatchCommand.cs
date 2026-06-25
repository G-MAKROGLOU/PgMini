namespace PgMini.Models;

/// <summary>A single command within a batch execution.</summary>
public sealed record BatchCommand(string Query, List<QueryParams>? Parameters = null);
