using PgMini.Models;

namespace PgMini.Tests.Integration;

[Collection("Postgres")]
public class ReadFirstOrDefaultAsyncTests(PostgresFixture db) : IAsyncLifetime
{
    public Task InitializeAsync() => db.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ReadFirstOrDefaultAsync_ReturnsFirstRow()
    {
        var row = await db.Client.ReadFirstOrDefaultAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple ORDER BY id");

        row.Should().NotBeNull();
        row!.Name.Should().Be("alpha");
    }

    [Fact]
    public async Task ReadFirstOrDefaultAsync_EmptySet_ReturnsNull()
    {
        var row = await db.Client.ReadFirstOrDefaultAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple WHERE name = 'nope'");

        row.Should().BeNull();
    }

    [Fact]
    public async Task ReadFirstOrDefaultAsync_MultipleMatches_ReturnsOnlyFirst()
    {
        var row = await db.Client.ReadFirstOrDefaultAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple ORDER BY id DESC");

        row!.Name.Should().Be("gamma"); // last alphabetically = first when ordered DESC
    }
}

[Collection("Postgres")]
public class ReadSingleAsyncTests(PostgresFixture db) : IAsyncLifetime
{
    public Task InitializeAsync() => db.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ReadSingleAsync_ExactlyOneRow_ReturnsIt()
    {
        var row = await db.Client.ReadSingleAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple WHERE name = 'beta'");

        row.Value.Should().Be(20);
    }

    [Fact]
    public async Task ReadSingleAsync_EmptySet_ThrowsInvalidOperation()
    {
        var act = async () => await db.Client.ReadSingleAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple WHERE name = 'ghost'");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no rows*");
    }

    [Fact]
    public async Task ReadSingleAsync_MultipleRows_ThrowsInvalidOperation()
    {
        var act = async () => await db.Client.ReadSingleAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*more than one row*");
    }
}
