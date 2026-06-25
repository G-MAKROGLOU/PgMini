using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace PgMini.Tests.Integration;

public class SimpleRow
{
    [Column("id")]    public int Id { get; set; }
    [Column("name")]  public string? Name { get; set; }
    [Column("value")] public int? Value { get; set; }
}

public class JsonRow
{
    [Column("id")]             public int Id { get; set; }
    [Column("name")]           public string? Name { get; set; }
    [Column("data", TypeName = "jsonb")] public JsonDocument? Data { get; set; }
}

public class CountResult
{
    [Column("count")] public long Count { get; set; }
}
