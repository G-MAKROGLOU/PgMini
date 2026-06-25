using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PgMini;
using Testcontainers.PostgreSql;

namespace PgMini.Tests.Integration;

/// <summary>
/// Spins up a single PostgreSQL container for the entire integration test run.
/// All tests in [Collection("Postgres")] share this fixture.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithDatabase("pgmini_test")
        .WithUsername("pgmini")
        .WithPassword("pgmini_pw")
        .Build();

    public DbClient Client { get; private set; } = null!;
    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        Client = new DbClient(ConnectionString, NullLogger<DbClient>.Instance);

        await SeedSchema();
    }

    private async Task SeedSchema()
    {
        var dataSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS test_simple (
                id         SERIAL PRIMARY KEY,
                name       TEXT        NOT NULL,
                value      INTEGER,
                created_at TIMESTAMPTZ DEFAULT NOW()
            );

            CREATE TABLE IF NOT EXISTS test_json (
                id   SERIAL PRIMARY KEY,
                name TEXT  NOT NULL,
                data JSONB
            );

            TRUNCATE TABLE test_simple RESTART IDENTITY CASCADE;
            TRUNCATE TABLE test_json   RESTART IDENTITY CASCADE;

            INSERT INTO test_simple (name, value) VALUES
                ('alpha', 10),
                ('beta',  20),
                ('gamma', 30);

            INSERT INTO test_json (name, data) VALUES
                ('first',  '{"key":"value","num":42}'),
                ('second', NULL);
            """, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ResetAsync()
    {
        var dataSource = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("""
            TRUNCATE TABLE test_simple RESTART IDENTITY CASCADE;
            TRUNCATE TABLE test_json   RESTART IDENTITY CASCADE;

            INSERT INTO test_simple (name, value) VALUES
                ('alpha', 10),
                ('beta',  20),
                ('gamma', 30);

            INSERT INTO test_json (name, data) VALUES
                ('first',  '{"key":"value","num":42}'),
                ('second', NULL);
            """, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await Client.DisposeAsync();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("Postgres")]
public class PostgresCollection : ICollectionFixture<PostgresFixture>;
