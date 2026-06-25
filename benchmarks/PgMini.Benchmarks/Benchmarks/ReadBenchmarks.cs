using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PgMini.Models;

namespace PgMini.Benchmarks.Benchmarks;

/// <summary>
/// Compares read performance across three access strategies:
///   - Raw Npgsql (baseline): manual reader loop, no abstraction overhead
///   - PgMini: attribute-mapped, reflection-cached column mapping
///   - Dapper: convention-based mapping via dynamic IL emit
///
/// Results interpretation:
///   - PgMini overhead vs Raw = the cost of attribute reflection caching + SetValue
///   - Dapper vs PgMini = Dapper uses IL emit (faster SetValue) but has its own overhead
///   - Memory (alloc column) matters for high-throughput scenarios
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class ReadBenchmarks : IDisposable
{
    private NpgsqlDataSource _dataSource = null!;
    private DbClient _client = null!;
    private NpgsqlConnection _dapperConn = null!;

    [Params(1, 10, 100, 1000)]
    public int Rows { get; set; }

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
            CREATE TABLE IF NOT EXISTS bench_read (
                id    SERIAL  PRIMARY KEY,
                name  TEXT    NOT NULL,
                value INTEGER NOT NULL
            );
            TRUNCATE TABLE bench_read RESTART IDENTITY;
            INSERT INTO bench_read (name, value)
                SELECT 'item_' || gs, gs
                FROM generate_series(1, 1000) gs;
            """, conn);
        await cmd.ExecuteNonQueryAsync();

        // Dapper reuses a single long-lived connection to match PgMini's pool behaviour.
        _dapperConn = await _dataSource.OpenConnectionAsync();
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _dapperConn.Dispose();
        _dataSource.Dispose();
    }

    // ── Benchmarks ─────────────────────────────────────────────────────────────

    /// <summary>Raw ADO.NET — the theoretical performance ceiling for this driver.</summary>
    [Benchmark(Baseline = true, Description = "Raw Npgsql")]
    public async Task<List<BenchRow>> RawNpgsql()
    {
        var rows = new List<BenchRow>(Rows);
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            $"SELECT id, name, value FROM bench_read LIMIT {Rows}", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new BenchRow
            {
                Id    = reader.GetInt32(0),
                Name  = reader.GetString(1),
                Value = reader.GetInt32(2)
            });
        }
        return rows;
    }

    [Benchmark(Description = "PgMini")]
    public Task<List<BenchRow>> PgMini() =>
        _client.ReadAsync<BenchRow>($"SELECT id, name, value FROM bench_read LIMIT {Rows}");

    [Benchmark(Description = "Dapper")]
    public async Task<IEnumerable<BenchRow>> Dapper() =>
        await _dapperConn.QueryAsync<BenchRow>(
            $"SELECT id, name, value FROM bench_read LIMIT {Rows}");
}
