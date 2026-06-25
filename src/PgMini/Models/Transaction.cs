namespace PgMini.Models;

/// <summary>
/// Describes a named group of <see cref="TransactionalQuery"/> steps that execute within
/// a single PostgreSQL transaction. Steps run sequentially; any exception triggers a rollback.
/// </summary>
public sealed class Transaction
{
    /// <summary>Human-readable name used in log output to identify this transaction group.</summary>
    public required string TransactionGroupName { get; init; }

    /// <summary>Ordered list of pipeline steps.</summary>
    public required List<TransactionalQuery> Queries { get; init; }
}
