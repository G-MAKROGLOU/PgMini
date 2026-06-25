using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PgMini.Helpers;
using PgMini.Models;
using Transaction = PgMini.Models.Transaction;

namespace PgMini.Benchmarks.Benchmarks;

/// <summary>
/// Compares a two-step transactional write (INSERT then SELECT scalar) between Raw Npgsql
/// and the PgMini transactional pipeline. Measures overhead of the pipeline abstraction
/// vs hand-written transaction management.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class TransactionBenchmarks : IDisposable
{
    private NpgsqlDataSource _dataSource = null!;
    private DbClient _client = null!;

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
            CREATE TABLE IF NOT EXISTS bench_tx (
                id    BIGSERIAL PRIMARY KEY,
                name  TEXT      NOT NULL,
                value INTEGER   NOT NULL
            );
            """, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    [GlobalCleanup]
    public void Dispose() => _dataSource.Dispose();

    // ── Benchmarks ─────────────────────────────────────────────────────────────

    [Benchmark(Baseline = true, Description = "Raw Npgsql")]
    public async Task<long> RawNpgsql()
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await using var insert = new NpgsqlCommand(
            "INSERT INTO bench_tx (name, value) VALUES (@n, @v)", conn, tx);
        insert.Parameters.AddWithValue("@n", "tx_bench");
        insert.Parameters.AddWithValue("@v", 1);
        await insert.ExecuteNonQueryAsync();

        await using var count = new NpgsqlCommand(
            "SELECT COUNT(*) FROM bench_tx", conn, tx);
        var result = (long)(await count.ExecuteScalarAsync())!;

        await tx.CommitAsync();
        return result;
    }

    [Benchmark(Description = "PgMini pipeline")]
    public async Task<long?> PgMiniPipeline()
    {
        var tx = new Transaction
        {
            TransactionGroupName = "bench",
            Queries =
            [
                new TransactionalQuery
                {
                    Runnable = _client.AutonomousTxExecuteStep(
                        "INSERT INTO bench_tx (name, value) VALUES (@name, @value)",
                        [QueryParams.Of("name", "tx_bench"), QueryParams.Of("value", 1)]),
                    PostExecute = TxHelper.NoOpExecute()
                },
                new TransactionalQuery
                {
                    Runnable = _client.AutonomousTxExecuteScalarStep<long>(
                        "SELECT COUNT(*) FROM bench_tx")
                }
            ]
        };

        return await _client.RunTransactional<long>(tx);
    }
}
