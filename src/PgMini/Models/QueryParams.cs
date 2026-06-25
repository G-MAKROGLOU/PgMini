namespace PgMini.Models;

/// <summary>Represents a named parameter for a parameterized SQL query.</summary>
public sealed record QueryParams
{
    /// <summary>Parameter name (without the leading <c>@</c>).</summary>
    public required string Key { get; init; }

    /// <summary>Parameter value. Pass <c>null</c> to bind SQL NULL.</summary>
    public required object? Value { get; init; }

    /// <summary>Convenience factory.</summary>
    public static QueryParams Of(string key, object? value) => new() { Key = key, Value = value };
}
