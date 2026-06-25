using PgMini.Models;

namespace PgMini.Tests.Integration;

[Collection("Postgres")]
public class ExistsAsyncTests(PostgresFixture db) : IAsyncLifetime
{
    public Task InitializeAsync() => db.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ExistsAsync_MatchingRow_ReturnsTrue()
    {
        var exists = await db.Client.ExistsAsync(
            "SELECT EXISTS(SELECT 1 FROM test_simple WHERE name = @name)",
            [QueryParams.Of("name", "alpha")]);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NoMatchingRow_ReturnsFalse()
    {
        var exists = await db.Client.ExistsAsync(
            "SELECT EXISTS(SELECT 1 FROM test_simple WHERE name = @name)",
            [QueryParams.Of("name", "ghost")]);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_AfterDelete_ReturnsFalse()
    {
        await db.Client.ExecuteAsync("DELETE FROM test_simple WHERE name = 'gamma'");

        var exists = await db.Client.ExistsAsync(
            "SELECT EXISTS(SELECT 1 FROM test_simple WHERE name = 'gamma')");

        exists.Should().BeFalse();
    }
}
