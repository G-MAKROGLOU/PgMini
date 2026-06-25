using PgMini.Models;

namespace PgMini.Tests.Integration;

[Collection("Postgres")]
public class ExecuteScalarAsyncTests(PostgresFixture db) : IAsyncLifetime
{
    public Task InitializeAsync() => db.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ExecuteScalarAsync_Count_ReturnsCorrectNumber()
    {
        var count = await db.Client.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM test_simple");

        count.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteScalarAsync_ReturningId_ReturnsInsertedId()
    {
        var id = await db.Client.ExecuteScalarAsync<int>(
            "INSERT INTO test_simple (name, value) VALUES (@name, @value) RETURNING id",
            [QueryParams.Of("name", "zeta"), QueryParams.Of("value", 60)]);

        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteScalarAsync_NoRows_ReturnsDefault()
    {
        var result = await db.Client.ExecuteScalarAsync<int?>(
            "SELECT value FROM test_simple WHERE name = 'ghost'");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteScalarAsync_Max_ReturnsMaxValue()
    {
        var max = await db.Client.ExecuteScalarAsync<int>("SELECT MAX(value) FROM test_simple");

        max.Should().Be(30);
    }
}

[Collection("Postgres")]
public class ExecuteBatchAsyncTests(PostgresFixture db) : IAsyncLifetime
{
    public Task InitializeAsync() => db.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ExecuteBatchAsync_MultiplInserts_AllRowsAppear()
    {
        var commands = new List<BatchCommand>
        {
            new("INSERT INTO test_simple (name, value) VALUES (@n1, @v1)",
                [QueryParams.Of("n1", "batch1"), QueryParams.Of("v1", 100)]),
            new("INSERT INTO test_simple (name, value) VALUES (@n2, @v2)",
                [QueryParams.Of("n2", "batch2"), QueryParams.Of("v2", 200)]),
            new("INSERT INTO test_simple (name, value) VALUES (@n3, @v3)",
                [QueryParams.Of("n3", "batch3"), QueryParams.Of("v3", 300)]),
        };

        var affected = await db.Client.ExecuteBatchAsync(commands);
        affected.Should().Be(3);

        var rows = await db.Client.ReadAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple WHERE name LIKE 'batch%' ORDER BY name");

        rows.Should().HaveCount(3);
        rows.Select(r => r.Name).Should().Equal("batch1", "batch2", "batch3");
    }

    [Fact]
    public async Task ExecuteBatchAsync_EmptyCommands_ReturnsZero()
    {
        var affected = await db.Client.ExecuteBatchAsync([]);

        affected.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteBatchAsync_MixedOperations_AllApplied()
    {
        var commands = new List<BatchCommand>
        {
            new("UPDATE test_simple SET value = 999 WHERE name = 'alpha'"),
            new("DELETE FROM test_simple WHERE name = 'beta'"),
        };

        await db.Client.ExecuteBatchAsync(commands);

        var alpha = await db.Client.ReadFirstOrDefaultAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple WHERE name = 'alpha'");
        alpha!.Value.Should().Be(999);

        var beta = await db.Client.ReadFirstOrDefaultAsync<SimpleRow>(
            "SELECT id, name, value FROM test_simple WHERE name = 'beta'");
        beta.Should().BeNull();
    }
}
