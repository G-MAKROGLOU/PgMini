using PgMini.Models;

namespace PgMini.Tests.Integration;

[Collection("Postgres")]
public class ReadAsyncTests(PostgresFixture db) : IAsyncLifetime
{
    public Task InitializeAsync() => db.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ReadAsync_ReturnsAllRows()
    {
        var rows = await db.Client.ReadAsync<SimpleRow>("SELECT id, name, value FROM test_simple ORDER BY id");

        rows.Should().HaveCount(3);
        rows[0].Name.Should().Be("alpha");
        rows[1].Name.Should().Be("beta");
        rows[2].Name.Should().Be("gamma");
    }

    [Fact]
    public async Task ReadAsync_WithParameters_FiltersCorrectly()
    {
        var rows = await db.Client.ReadAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple WHERE name = @name",
            [QueryParams.Of("name", "beta")]);

        rows.Should().HaveCount(1);
        rows[0].Value.Should().Be(20);
    }

    [Fact]
    public async Task ReadAsync_EmptyResultSet_ReturnsEmptyList()
    {
        var rows = await db.Client.ReadAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple WHERE name = @name",
            [QueryParams.Of("name", "nonexistent")]);

        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_NullValue_MapsToNull()
    {
        // value column can be null — insert a row without it
        await db.Client.ExecuteAsync("INSERT INTO test_simple (name) VALUES ('nullval')");

        var rows = await db.Client.ReadAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple WHERE name = 'nullval'");

        rows.Should().HaveCount(1);
        rows[0].Value.Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_JsonbColumn_DeserializesJsonDocument()
    {
        var rows = await db.Client.ReadAsync<JsonRow>(
            "SELECT id, name, data FROM test_json WHERE name = 'first'");

        rows.Should().HaveCount(1);
        rows[0].Data.Should().NotBeNull();
        rows[0].Data!.RootElement.GetProperty("key").GetString().Should().Be("value");
        rows[0].Data!.RootElement.GetProperty("num").GetInt32().Should().Be(42);
    }

    [Fact]
    public async Task ReadAsync_JsonbNullColumn_MapsToNull()
    {
        var rows = await db.Client.ReadAsync<JsonRow>(
            "SELECT id, name, data FROM test_json WHERE name = 'second'");

        rows.Should().HaveCount(1);
        rows[0].Data.Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_NullParameter_BindsSqlNull()
    {
        // Seed a null-value row so this test is self-contained regardless of execution order
        await db.Client.ExecuteAsync("INSERT INTO test_simple (name) VALUES ('nullval_param')");

        var rows = await db.Client.ReadAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple WHERE value IS NOT DISTINCT FROM @val",
            [QueryParams.Of("val", null)]);

        rows.Should().NotBeEmpty();
        rows.Should().AllSatisfy(r => r.Value.Should().BeNull());
    }

    [Fact]
    public async Task ReadAsync_MultipleParameters_AllApplied()
    {
        var rows = await db.Client.ReadAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple WHERE name = @name AND value = @value",
            [QueryParams.Of("name", "alpha"), QueryParams.Of("value", 10)]);

        rows.Should().HaveCount(1);
        rows[0].Id.Should().Be(1);
    }
}
