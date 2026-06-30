# PgMini

[![NuGet](https://img.shields.io/nuget/v/PgMini.svg)](https://www.nuget.org/packages/PgMini)
[![CI](https://github.com/G-MAKROGLOU/PgMini/actions/workflows/ci.yml/badge.svg)](https://github.com/G-MAKROGLOU/PgMini/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A lightweight, pipeline-driven PostgreSQL client for .NET 8+.

You write SQL. PgMini maps results to typed models, manages parameters, and handles transactions - nothing more.

---

## Why PgMini?

| | PgMini | Dapper | EF Core |
|---|---|---|---|
| You write SQL | ✅ Always | ✅ Always | ⚠️ Optional (LINQ) |
| Column mapping | `[Column]` attribute | Convention / attribute | `[Column]` / Fluent API |
| Transactional pipeline | ✅ Built-in | ✗ Manual | ⚠️ SaveChanges |
| JSONB support | ✅ `JsonDocument` | Manual | Extension |
| Change tracking | ✗ None | ✗ None | ✅ Full |
| Migrations | ✗ None | ✗ None | ✅ Full |
| Setup | `AddPgMini(connStr)` | Manual | `AddDbContext(...)` |

**PgMini is for you if** you want full SQL control, lightweight mapping, and a structured way to compose multi-step transactions - without the ceremony of a full ORM.

**PgMini is not for you if** you need LINQ queries, entity relationships, or migration tooling - use EF Core instead.

---

## Installation

```bash
dotnet add package PgMini
```

Requires .NET 8+.

---

## Quick Start

### 1. Register

```csharp
// Program.cs - single database
builder.Services.AddPgMini("Host=localhost;Database=mydb;Username=user;Password=pass");

// Or pull from config
builder.Services.AddPgMini(builder.Configuration.GetConnectionString("Default")!);
```

For multiple databases (multi-tenant, read replicas, etc.):

```csharp
builder.Services.AddNamedPgMini("primary",   config["DB_PRIMARY"]!);
builder.Services.AddNamedPgMini("analytics", config["DB_ANALYTICS"]!);
```

### 2. Define your model

Annotate each property you want mapped with `[Column("db_column_name")]`:

```csharp
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

public class Vessel
{
    [Column("vessel_id")]                          public int           Id       { get; set; }
    [Column("vessel_name")]                        public string?       Name     { get; set; }
    [Column("imo_number")]                         public string?       Imo      { get; set; }
    [Column("gross_tonnage")]                      public int?          Tonnage  { get; set; }
    [Column("metadata", TypeName = "jsonb")]       public JsonDocument? Metadata { get; set; }
}
```

Properties without `[Column]` are silently ignored. Nullable properties are set to `null` when the DB value is `NULL`.

### 3. Inject and query

```csharp
public class VesselService(IDbClient db)
{
    // All rows
    public Task<List<Vessel>> GetAllAsync() =>
        db.ReadAsync<Vessel>("SELECT vessel_id, vessel_name, imo_number FROM vessels");

    // First or null
    public Task<Vessel?> FindByImoAsync(string imo) =>
        db.ReadFirstOrDefaultAsync<Vessel>(
            "SELECT vessel_id, vessel_name, imo_number FROM vessels WHERE imo_number = @imo",
            [QueryParams.Of("imo", imo)]);

    // Exactly one row - throws if 0 or 2+
    public Task<Vessel> GetByIdAsync(int id) =>
        db.ReadSingleAsync<Vessel>(
            "SELECT vessel_id, vessel_name, imo_number FROM vessels WHERE vessel_id = @id",
            [QueryParams.Of("id", id)]);

    // Existence check
    public Task<bool> ImoExistsAsync(string imo) =>
        db.ExistsAsync(
            "SELECT EXISTS(SELECT 1 FROM vessels WHERE imo_number = @imo)",
            [QueryParams.Of("imo", imo)]);

    // Insert / update / delete
    public Task<int> InsertAsync(Vessel v) =>
        db.ExecuteAsync(
            "INSERT INTO vessels (vessel_name, imo_number) VALUES (@name, @imo)",
            [QueryParams.Of("name", v.Name), QueryParams.Of("imo", v.Imo)]);

    // Scalar value
    public Task<int?> CountAsync() =>
        db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM vessels");
}
```

For named clients:

```csharp
public class AnalyticsService([FromKeyedServices("analytics")] IDbClient db)
{
    public Task<List<VoyageStats>> GetStatsAsync() =>
        db.ReadAsync<VoyageStats>("SELECT ...");
}
```

---

## Pagination

```csharp
// Appends LIMIT / OFFSET automatically - do NOT include them in your query.
var page = await db.ReadPagedAsync<Vessel>(
    "SELECT vessel_id, vessel_name FROM vessels ORDER BY vessel_name",
    pageSize: 20,
    offset: 40);   // or: (pageNumber - 1) * pageSize
```

---

## Streaming large result sets

Use `StreamAsync<T>` when you do not want to buffer all rows into a `List<T>` first - ideal for exports, batch processing, or very large tables:

```csharp
await foreach (var vessel in db.StreamAsync<Vessel>(
    "SELECT vessel_id, vessel_name FROM vessels ORDER BY vessel_id",
    cancellationToken: ct))
{
    await writer.WriteAsync(vessel);
}
```

Rows are yielded as they arrive from the driver. The connection is held open for the duration of the iteration and closed when you break or exhaust the enumerator.

---

## Batch operations

Send multiple non-query commands in a single round-trip:

```csharp
var affected = await db.ExecuteBatchAsync(
[
    new BatchCommand(
        "INSERT INTO vessels (vessel_name, imo_number) VALUES (@name, @imo)",
        [QueryParams.Of("name", "MV Aurora"), QueryParams.Of("imo", "IMO1234567")]),
    new BatchCommand(
        "UPDATE fleets SET vessel_count = vessel_count + 1 WHERE fleet_id = @id",
        [QueryParams.Of("id", fleetId)]),
]);
```

---

## Transactional Pipeline

The core design of PgMini. Chain typed steps inside a single PostgreSQL transaction; if any step throws, the whole transaction rolls back automatically.

### Two-step example: insert then read back

```csharp
var tx = new Transaction
{
    TransactionGroupName = "RegisterVessel",
    Queries =
    [
        new TransactionalQuery
        {
            // Step 1: insert and get the new ID via RETURNING
            Runnable = db.AutonomousTxExecuteScalarStep<int>(
                "INSERT INTO vessels (vessel_name, imo_number) VALUES (@name, @imo) RETURNING vessel_id",
                [QueryParams.Of("name", "MV Nordic Star"), QueryParams.Of("imo", "IMO9876543")]),

            // Forward the ID to the next step
            PostExecuteScalar = result =>
            {
                var id = (int)result;
                return Task.FromResult<(string?, List<QueryParams>?)>((
                    "SELECT vessel_id, vessel_name, imo_number FROM vessels WHERE vessel_id = @id",
                    [QueryParams.Of("id", id)]
                ));
            }
        },
        new TransactionalQuery
        {
            // Step 2: read the newly inserted row using the forwarded query + params
            Runnable = db.TxReadStep<Vessel>()
        }
    ]
};

var vessels = await db.RunTransactional<List<Vessel>>(tx);
var vessel  = vessels?.FirstOrDefault();
```

### Controlling isolation level

```csharp
var result = await db.RunTransactional<int>(tx, IsolationLevel.Serializable);
```

Default is `ReadCommitted`.

### Step helpers

| Method | When to use |
|--------|-------------|
| `TxReadStep<T>()` | Read using query forwarded from the previous step |
| `AutonomousTxReadStep<T>(sql, params)` | Read with a fixed query - ignores forwarded values |
| `TxExecuteStep()` | Execute using forwarded query/params |
| `AutonomousTxExecuteStep(sql, params)` | Execute with a fixed query |
| `TxExecuteScalarStep<T>()` | Scalar using forwarded query/params |
| `AutonomousTxExecuteScalarStep<T>(sql, params)` | Scalar with a fixed query |

Use `TxHelper.NoOp()` / `TxHelper.NoOpExecute()` as `PostRead` / `PostExecute` when a step does not need to forward anything to the next step.

### Three-step example: multi-entity create

```csharp
var tx = new Transaction
{
    TransactionGroupName = "CreateVoyage",
    Queries =
    [
        new TransactionalQuery
        {
            Runnable    = db.AutonomousTxExecuteStep(
                "INSERT INTO voyages (vessel_id, departure_port) VALUES (@vid, @port)",
                [QueryParams.Of("vid", vesselId), QueryParams.Of("port", "NLRTM")]),
            PostExecute = TxHelper.NoOpExecute()
        },
        new TransactionalQuery
        {
            Runnable    = db.AutonomousTxExecuteStep(
                "UPDATE vessels SET active_voyage_count = active_voyage_count + 1 WHERE vessel_id = @vid",
                [QueryParams.Of("vid", vesselId)]),
            PostExecute = TxHelper.NoOpExecute()
        },
        new TransactionalQuery
        {
            Runnable = db.AutonomousTxExecuteScalarStep<int>(
                "SELECT active_voyage_count FROM vessels WHERE vessel_id = @vid",
                [QueryParams.Of("vid", vesselId)])
        }
    ]
};

var activeVoyages = await db.RunTransactional<int>(tx);
```

---

## Dependency Injection

### Single database

```csharp
// Program.cs
builder.Services.AddPgMini(connectionString);

// Optional: customise the NpgsqlDataSource
builder.Services.AddPgMini(connectionString, dataSourceBuilder =>
{
    dataSourceBuilder.EnableDynamicJson();
    dataSourceBuilder.MapEnum<VesselStatus>("vessel_status");
});
```

### Multiple databases (keyed services - .NET 8+)

```csharp
builder.Services.AddNamedPgMini("ops",       config["DB_OPS"]!);
builder.Services.AddNamedPgMini("reporting", config["DB_REPORTING"]!);
```

```csharp
// Injected via [FromKeyedServices]
public class ReportingService([FromKeyedServices("reporting")] IDbClient db) { ... }
```

---

## API Reference

### `IDbClient`

| Method | Returns | Description |
|--------|---------|-------------|
| `ReadAsync<T>(query, params?, tx?, ct?)` | `Task<List<T>>` | All matching rows |
| `ReadFirstOrDefaultAsync<T>(query, params?, tx?, ct?)` | `Task<T?>` | First row or `null` |
| `ReadSingleAsync<T>(query, params?, tx?, ct?)` | `Task<T>` | Exactly one row; throws if 0 or 2+ |
| `ReadPagedAsync<T>(query, pageSize, offset?, params?, tx?, ct?)` | `Task<List<T>>` | One page - appends `LIMIT`/`OFFSET` |
| `StreamAsync<T>(query, params?, ct?)` | `IAsyncEnumerable<T>` | Streaming row-by-row, no full buffer |
| `ExistsAsync(query, params?, tx?, ct?)` | `Task<bool>` | Use with `SELECT EXISTS(...)` |
| `ExecuteAsync(query, params?, tx?, ct?)` | `Task<int>` | Rows affected |
| `ExecuteBatchAsync(commands, ct?)` | `Task<int>` | Total rows affected across all commands |
| `ExecuteScalarAsync<T>(query, params?, tx?, ct?)` | `Task<T?>` | Single scalar value |
| `RunTransactional<T>(tx, isolationLevel?, ct?)` | `Task<T?>` | Full step pipeline in one transaction |

All methods accept an optional `NpgsqlTransaction` to participate in an externally-managed transaction, and an optional `CancellationToken`.

### `QueryParams`

```csharp
// Factory method - preferred
QueryParams.Of("vessel_id", 42)

// Record initialiser
new QueryParams { Key = "vessel_id", Value = 42 }
```

Pass `null` as `Value` to bind SQL `NULL`.

### `[Column]` attribute

```csharp
[Column("db_column_name")]               // standard column - maps by name
[Column("payload", TypeName = "jsonb")]  // JSONB column - property type must be JsonDocument / JsonDocument?
```

Use `System.ComponentModel.DataAnnotations.Schema.ColumnAttribute`. Properties without `[Column]` are ignored. Column name matching is case-insensitive.

---

## Performance

PgMini uses compiled Expression Trees to map query results to your models - the same technique used by Dapper. Mappings are compiled once per CLR type and cached; subsequent calls pay zero reflection cost.

### How it works

1. **First query for a type** - PgMini inspects the `[Column]` attributes once, compiles a typed setter delegate per property (`reader.GetFieldValue<T>(ordinal)` → direct property assignment), and caches them in a `ConcurrentDictionary<Type, ColumnMapping[]>`.
2. **Every subsequent query** - ordinals are resolved from the result schema (once per query, outside the row loop), then the compiled delegates are called directly. No `PropertyInfo.SetValue`, no `GetValue` boxing.

### Benchmark projections (local PostgreSQL, .NET 8)

Read `N` rows from a 3-column table (`int`, `string`, `int?`):

| Rows | Raw ADO.NET | Dapper | **PgMini** | EF Core (no tracking) | EF Core |
|------|-------------|--------|------------|----------------------|---------|
| 1    | ~350 μs     | ~375 μs | **~385 μs** | ~520 μs | ~630 μs |
| 10   | ~450 μs     | ~480 μs | **~490 μs** | ~720 μs | ~950 μs |
| 100  | ~1.1 ms     | ~1.2 ms | **~1.1 ms** | ~2.2 ms | ~3.1 ms |
| 1000 | ~7.3 ms     | ~7.6 ms | **~7.9 ms** | ~19 ms  | ~31 ms  |

Single `INSERT`:

| Library | Mean time | vs Raw |
|---------|-----------|--------|
| Raw ADO.NET | ~480 μs | baseline |
| Dapper | ~530 μs | +10% |
| **PgMini** | **~545 μs** | **+14%** |
| EF Core | ~920 μs | +92% |

> These are projected estimates based on known overhead characteristics. Run the included benchmark project against your own database to get accurate numbers for your environment.

### Running benchmarks

```bash
# Set a connection string pointing at an empty PostgreSQL database
$env:PGMINI_BENCH_CONNSTR = "Host=localhost;Database=pgmini_bench;Username=postgres;Password=postgres"

cd benchmarks/PgMini.Benchmarks
dotnet run -c Release                          # all scenarios
dotnet run -c Release -- --filter "*Read*"    # reads only
dotnet run -c Release -- --filter "*Write*"   # writes only
```

The benchmark project creates and seeds its own tables. Three scenarios are included: `ReadBenchmarks` (1/10/100/1000 rows), `WriteBenchmarks` (single INSERT), and `TransactionBenchmarks` (two-step pipeline).

---

## Versioning

Versions are derived automatically from git tags via [MinVer](https://github.com/adamralph/minver). Automated releases are managed by [Release Please](https://github.com/googleapis/release-please) using conventional commits:

| Commit prefix | Version bump |
|---------------|-------------|
| `fix: ...` | patch - `1.0.1` |
| `feat: ...` | minor - `1.1.0` |
| `feat!: ...` or `BREAKING CHANGE:` footer | major - `2.0.0` |

Push to `main` with conventional commits → Release Please opens a release PR. Merging the PR creates a version tag → the publish workflow pushes the package to NuGet automatically.

To trigger a manual release:

```bash
git tag v1.0.0
git push origin v1.0.0
```

---

## Contributing

1. Fork and create a feature branch
2. Use conventional commit messages (`feat:`, `fix:`, `perf:`, etc.)
3. Ensure `dotnet test` passes - integration tests require Docker (Testcontainers spins up PostgreSQL automatically)
4. Open a pull request against `main`

---

## License

MIT - see [LICENSE](LICENSE).
