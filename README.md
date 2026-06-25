# PgMini

[![NuGet](https://img.shields.io/nuget/v/PgMini.svg)](https://www.nuget.org/packages/PgMini)
[![CI](https://github.com/gmakroglou/PgMini/actions/workflows/ci.yml/badge.svg)](https://github.com/gmakroglou/PgMini/actions/workflows/ci.yml)

A lightweight, pipeline-driven PostgreSQL client for .NET 8+.

- **Attribute-based column mapping** — decorate your model properties with `[Column("db_col")]`, done.
- **Composable transactional pipeline** — chain query steps with typed delegates; rollback is automatic.
- **No ORM magic** — you write the SQL, PgMini handles the mapping, parameters, and disposal.
- **Multi-tenant ready** — register named clients via `AddNamedPgMini`, inject with `[FromKeyedServices]`.

---

## Installation

```bash
dotnet add package PgMini
```

---

## Quick Start

### 1. Register

```csharp
// Program.cs
builder.Services.AddPgMini("Host=localhost;Database=mydb;Username=user;Password=pass");
```

For multiple databases:

```csharp
builder.Services.AddNamedPgMini("portal",    Environment.GetEnvironmentVariable("PORTAL_DB")!);
builder.Services.AddNamedPgMini("analytics", Environment.GetEnvironmentVariable("ANALYTICS_DB")!);
```

### 2. Define your model

```csharp
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

public class Ship
{
    [Column("ship_id")]          public int Id { get; set; }
    [Column("ship_name")]        public string? Name { get; set; }
    [Column("ship_imo")]         public string? Imo { get; set; }
    [Column("ship_meta", TypeName = "jsonb")] public JsonDocument? Meta { get; set; }
}
```

### 3. Query

```csharp
public class ShipService(IDbClient db)
{
    public Task<List<Ship>> GetAllAsync() =>
        db.ReadAsync<Ship>("SELECT ship_id, ship_name, ship_imo, ship_meta FROM ships");

    public Task<Ship?> GetByImoAsync(string imo) =>
        db.ReadFirstOrDefaultAsync<Ship>(
            "SELECT ship_id, ship_name, ship_imo, ship_meta FROM ships WHERE ship_imo = @imo",
            [QueryParams.Of("imo", imo)]);

    public Task<bool> ExistsAsync(string imo) =>
        db.ExistsAsync(
            "SELECT EXISTS(SELECT 1 FROM ships WHERE ship_imo = @imo)",
            [QueryParams.Of("imo", imo)]);

    public Task<int> InsertAsync(Ship ship) =>
        db.ExecuteAsync(
            "INSERT INTO ships (ship_name, ship_imo) VALUES (@name, @imo)",
            [QueryParams.Of("name", ship.Name), QueryParams.Of("imo", ship.Imo)]);
}
```

---

## Transactional Pipeline

Chain steps inside a single transaction using delegates. Each step optionally forwards a query + parameters to the next step.

```csharp
// Insert a ship and immediately read it back, all in one transaction.
var tx = new Transaction
{
    TransactionGroupName = "RegisterShip",
    Queries =
    [
        new TransactionalQuery
        {
            Runnable = db.AutonomousTxExecuteScalarStep<int>(
                "INSERT INTO ships (ship_name) VALUES (@name) RETURNING ship_id",
                [QueryParams.Of("name", "MV Nordic Star")]),

            PostExecuteScalar = result =>
            {
                var newId = (int)result;
                return Task.FromResult<(string?, List<QueryParams>?)>((
                    "SELECT ship_id, ship_name FROM ships WHERE ship_id = @id",
                    [QueryParams.Of("id", newId)]
                ));
            }
        },
        new TransactionalQuery
        {
            Runnable = db.TxReadStep<Ship>()   // picks up the forwarded query & params
        }
    ]
};

var ship = await db.RunTransactional<List<Ship>>(tx);
```

If any step throws, the entire transaction is rolled back automatically.

### Step helpers

| Method | Description |
|--------|-------------|
| `TxReadStep<T>()` | READ using forwarded query/params |
| `AutonomousTxReadStep<T>(sql, params)` | READ with a fixed query |
| `TxExecuteStep()` | EXECUTE using forwarded query/params |
| `AutonomousTxExecuteStep(sql, params)` | EXECUTE with a fixed query |
| `TxExecuteScalarStep<T>()` | SCALAR using forwarded query/params |
| `AutonomousTxExecuteScalarStep<T>(sql, params)` | SCALAR with a fixed query |

Use `TxHelper.NoOp()` / `TxHelper.NoOpExecute()` as post-actions when a step doesn't need to forward anything.

---

## API Reference

### `IDbClient`

| Method | Returns | Notes |
|--------|---------|-------|
| `ReadAsync<T>` | `List<T>` | All matching rows |
| `ReadFirstOrDefaultAsync<T>` | `T?` | First row or null |
| `ReadSingleAsync<T>` | `T` | Exactly one row; throws otherwise |
| `ExistsAsync` | `bool` | Use with `SELECT EXISTS(...)` |
| `ExecuteAsync` | `int` | Affected row count |
| `ExecuteBatchAsync` | `int` | Total affected rows across all commands |
| `ExecuteScalarAsync<T>` | `T?` | Single scalar value |
| `RunTransactional<T>` | `T?` | Full pipeline in one transaction |

All methods accept an optional `NpgsqlTransaction` to participate in an outer transaction, and a `CancellationToken`.

### `[Column]` attribute

Use `System.ComponentModel.DataAnnotations.Schema.ColumnAttribute`:

```csharp
[Column("db_column_name")]              // standard column
[Column("payload", TypeName = "jsonb")] // JSONB column → JsonDocument
```

---

## Versioning & Publishing

Versions are derived automatically from git tags via [MinVer](https://github.com/adamralph/minver).

```bash
git tag v1.0.0
git push origin v1.0.0   # triggers the publish workflow
```

Set `NUGET_API_KEY` in your GitHub repository secrets before pushing a tag.

---

## License

MIT
