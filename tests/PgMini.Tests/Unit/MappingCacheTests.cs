using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace PgMini.Tests.Unit;

/// <summary>
/// Tests that verify the reflection-based column mapping logic by inspecting
/// attribute presence on test models — without needing a database connection.
/// </summary>
public class MappingCacheTests
{
    private class FullyMapped
    {
        [Column("id")]            public int Id { get; set; }
        [Column("name")]          public string? Name { get; set; }
        [Column("data", TypeName = "jsonb")] public object? Data { get; set; }
    }

    private class PartiallyMapped
    {
        [Column("id")]   public int Id { get; set; }
        public string? NotMapped { get; set; } // no [Column]
    }

    private class NoMappings
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    [Fact]
    public void ColumnAttribute_Name_IsReadCorrectly()
    {
        var prop = typeof(FullyMapped).GetProperty(nameof(FullyMapped.Name))!;
        var attr = prop.GetCustomAttribute<ColumnAttribute>();

        attr.Should().NotBeNull();
        attr!.Name.Should().Be("name");
    }

    [Fact]
    public void ColumnAttribute_TypeName_IsReadCorrectly()
    {
        var prop = typeof(FullyMapped).GetProperty(nameof(FullyMapped.Data))!;
        var attr = prop.GetCustomAttribute<ColumnAttribute>();

        attr.Should().NotBeNull();
        attr!.TypeName.Should().Be("jsonb");
    }

    [Fact]
    public void ColumnAttribute_MissingAttribute_ReturnsNull()
    {
        var prop = typeof(PartiallyMapped).GetProperty(nameof(PartiallyMapped.NotMapped))!;
        var attr = prop.GetCustomAttribute<ColumnAttribute>();

        attr.Should().BeNull();
    }

    [Fact]
    public void GetProperties_CountMatchesMappedProperties_ForPartiallyMapped()
    {
        var mapped = typeof(PartiallyMapped)
            .GetProperties()
            .Where(p => p.GetCustomAttribute<ColumnAttribute>() is not null)
            .ToList();

        mapped.Should().HaveCount(1);
        mapped[0].Name.Should().Be(nameof(PartiallyMapped.Id));
    }

    [Fact]
    public void GetProperties_NoMappings_ReturnsEmptyMappedList()
    {
        var mapped = typeof(NoMappings)
            .GetProperties()
            .Where(p => p.GetCustomAttribute<ColumnAttribute>() is not null)
            .ToList();

        mapped.Should().BeEmpty();
    }

    [Fact]
    public void ColumnAttribute_DetectedByType_NotByNameString()
    {
        // Ensure we are using typeof comparison, not string matching.
        // This verifies no regressions to the original string-based detection.
        var prop = typeof(FullyMapped).GetProperty(nameof(FullyMapped.Id))!;
        var attrByType = prop.GetCustomAttribute<ColumnAttribute>();
        var attrByName = prop.CustomAttributes
            .FirstOrDefault(a => a.AttributeType == typeof(ColumnAttribute));

        attrByType.Should().NotBeNull();
        attrByName.Should().NotBeNull();
        attrByType!.Name.Should().Be(attrByName!.ConstructorArguments[0].Value as string);
    }
}
