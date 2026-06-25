using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PgMini.Models;

namespace PgMini.Benchmarks.Benchmarks;

/// <summary>
/// Compares single-row INSERT performance across Raw Npgsql, PgMini, and Dapper.
/// Each benchmark method is async to account for the actual I/O cost; BenchmarkDotNet
/// measures wall-clock time including the round-trip to PostgreSQL.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class WriteBenchmarks : IDisposable
{
    private NpgsqlDataSource _dataSource = null!;
    private DbClient _client = null!;
    private NpgsqlConnection _dapperConn = null!;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        var connStr = Environment.GetEnvironmentVariable("PGMINI_BENCH_CONNSTR")
            ?? throw new InvalidOperationException(
                "Set PGMINI_BENCH_CONNSTR. Example: Host=localhost;Database=pgmini_bench;Username=postgres;Password=postgres");

        _dataSource = new NpgsqlDataSourceBuilder(connStr).EnableDynamicJson().Build();
        _client = new DbClient(_dataSource, NullLogger<DbClient>.Instance);

        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS bench_write (
                id    BIGSERIAL PRIMARY KEY,
                name  TEXT      NOT NULL,
                value INTEGER   NOT NULL
            );
            """, conn);
        await cmd.ExecuteNonQueryAsync();

        _dapperConn = await _dataSource.OpenConnectionAsync();
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _dapperConn.Dispose();
        _dataSource.Dispose();
    }

    // ── Benchmarks ─────────────────────────────────────────────────────────────

    [Benchmark(Baseline = true, Description = "Raw Npgsql")]
    public async Task RawNpgsql()
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO bench_write (name, value) VALUES (@n, @v)", conn);
        cmd.Parameters.AddWithValue("@n", "bench");
        cmd.Parameters.AddWithValue("@v", 99);
        await cmd.ExecuteNonQueryAsync();
    }

    [Benchmark(Description = "PgMini")]
    public Task PgMini() =>
        _client.ExecuteAsync(
            "INSERT INTO bench_write (name, value) VALUES (@name, @value)",
            [QueryParams.Of("name", "bench"), QueryParams.Of("value", 99)]);

    [Benchmark(Description = "Dapper")]
    public Task Dapper() =>
        _dapperConn.ExecuteAsync(
            "INSERT INTO bench_write (name, value) VALUES (@Name, @Value)",
            new { Name = "bench", Value = 99 });
}
