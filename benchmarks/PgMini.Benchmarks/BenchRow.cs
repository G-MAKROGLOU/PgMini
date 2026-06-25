using System.ComponentModel.DataAnnotations.Schema;

namespace PgMini.Benchmarks;

/// <summary>
/// Shared model used by PgMini (via [Column]) and Dapper (case-insensitive property name match).
/// </summary>
public class BenchRow
{
    [Column("id")]    public int    Id    { get; set; }
    [Column("name")]  public string Name  { get; set; } = "";
    [Column("value")] public int    Value { get; set; }
}
