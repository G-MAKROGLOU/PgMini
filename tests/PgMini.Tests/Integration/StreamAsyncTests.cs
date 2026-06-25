using PgMini.Models;

namespace PgMini.Tests.Integration;

[Collection("Postgres")]
public class StreamAsyncTests(PostgresFixture db) : IAsyncLifetime
{
    public Task InitializeAsync() => db.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task StreamAsync_AllRows_YieldsAllRows()
    {
        var rows = new List<SimpleRow>();
        await foreach (var row in db.Client.StreamAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple ORDER BY id"))
        {
            rows.Add(row);
        }

        rows.Should().HaveCount(3);
        rows[0].Name.Should().Be("alpha");
        rows[2].Name.Should().Be("gamma");
    }

    [Fact]
    public async Task StreamAsync_WithParameters_FiltersCorrectly()
    {
        var rows = new List<SimpleRow>();
        await foreach (var row in db.Client.StreamAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple WHERE name = @name",
            [QueryParams.Of("name", "beta")]))
        {
            rows.Add(row);
        }

        rows.Should().HaveCount(1);
        rows[0].Value.Should().Be(20);
    }

    [Fact]
    public async Task StreamAsync_EmptyResult_YieldsNoRows()
    {
        var rows = new List<SimpleRow>();
        await foreach (var row in db.Client.StreamAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple WHERE name = 'nonexistent'"))
        {
            rows.Add(row);
        }

        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task StreamAsync_EarlyBreak_DoesNotThrow()
    {
        // Consumer breaks out of the stream early — the iterator should still clean up correctly.
        SimpleRow? first = null;
        await foreach (var row in db.Client.StreamAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple ORDER BY id"))
        {
            first = row;
            break;
        }

        first.Should().NotBeNull();
        first!.Name.Should().Be("alpha");
    }

    [Fact]
    public async Task StreamAsync_CancellationToken_Cancels()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = async () =>
        {
            await foreach (var _ in db.Client.StreamAsync<SimpleRow>(
                "SELECT id, name, value FROM test_simple",
                cancellationToken: cts.Token))
            {
                // Should not reach here
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
