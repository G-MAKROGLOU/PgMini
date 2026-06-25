using PgMini.Models;

namespace PgMini.Tests.Integration;

[Collection("Postgres")]
public class ReadPagedAsyncTests(PostgresFixture db) : IAsyncLifetime
{
    public Task InitializeAsync() => db.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ReadPagedAsync_FirstPage_ReturnsPageSizeRows()
    {
        var rows = await db.Client.ReadPagedAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple ORDER BY id",
            pageSize: 2,
            offset: 0);

        rows.Should().HaveCount(2);
        rows[0].Name.Should().Be("alpha");
        rows[1].Name.Should().Be("beta");
    }

    [Fact]
    public async Task ReadPagedAsync_SecondPage_ReturnsCorrectOffset()
    {
        var rows = await db.Client.ReadPagedAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple ORDER BY id",
            pageSize: 2,
            offset: 2);

        rows.Should().HaveCount(1);
        rows[0].Name.Should().Be("gamma");
    }

    [Fact]
    public async Task ReadPagedAsync_PageBeyondEnd_ReturnsEmpty()
    {
        var rows = await db.Client.ReadPagedAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple ORDER BY id",
            pageSize: 10,
            offset: 100);

        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadPagedAsync_WithParameters_FiltersBeforePaging()
    {
        // Seed extra rows so pagination is meaningful
        await db.Client.ExecuteAsync("""
            INSERT INTO test_simple (name, value) VALUES
                ('alpha2', 10),
                ('alpha3', 10),
                ('alpha4', 10)
            """);

        var rows = await db.Client.ReadPagedAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple WHERE value = @val ORDER BY id",
            pageSize: 2,
            offset: 0,
            parameters: [QueryParams.Of("val", 10)]);

        rows.Should().HaveCount(2);
        rows.Should().AllSatisfy(r => r.Value.Should().Be(10));
    }

    [Fact]
    public async Task ReadPagedAsync_TrailingSemicolon_HandledGracefully()
    {
        // Callers who accidentally include a semicolon should still get correct results.
        var rows = await db.Client.ReadPagedAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple ORDER BY id;",
            pageSize: 2,
            offset: 0);

        rows.Should().HaveCount(2);
    }
}
