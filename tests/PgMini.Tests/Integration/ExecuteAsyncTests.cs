using PgMini.Models;

namespace PgMini.Tests.Integration;

[Collection("Postgres")]
public class ExecuteAsyncTests(PostgresFixture db) : IAsyncLifetime
{
    public Task InitializeAsync() => db.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ExecuteAsync_Insert_ReturnsOneAffectedRow()
    {
        var affected = await db.Client.ExecuteAsync(
            "INSERT INTO test_simple (name, value) VALUES (@name, @value)",
            [QueryParams.Of("name", "delta"), QueryParams.Of("value", 40)]);

        affected.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_Insert_RowAppearsOnSubsequentRead()
    {
        await db.Client.ExecuteAsync(
            "INSERT INTO test_simple (name, value) VALUES ('epsilon', 50)");

        var row = await db.Client.ReadFirstOrDefaultAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple WHERE name = 'epsilon'");

        row.Should().NotBeNull();
        row!.Value.Should().Be(50);
    }

    [Fact]
    public async Task ExecuteAsync_Update_ChangesExistingRow()
    {
        await db.Client.ExecuteAsync(
            "UPDATE test_simple SET value = @val WHERE name = 'alpha'",
            [QueryParams.Of("val", 999)]);

        var row = await db.Client.ReadFirstOrDefaultAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple WHERE name = 'alpha'");

        row!.Value.Should().Be(999);
    }

    [Fact]
    public async Task ExecuteAsync_Delete_RowDisappears()
    {
        await db.Client.ExecuteAsync(
            "DELETE FROM test_simple WHERE name = 'gamma'");

        var rows = await db.Client.ReadAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple");

        rows.Should().NotContain(r => r.Name == "gamma");
    }

    [Fact]
    public async Task ExecuteAsync_NoMatchingRows_ReturnsZero()
    {
        var affected = await db.Client.ExecuteAsync(
            "DELETE FROM test_simple WHERE name = 'nonexistent'");

        affected.Should().Be(0);
    }
}
