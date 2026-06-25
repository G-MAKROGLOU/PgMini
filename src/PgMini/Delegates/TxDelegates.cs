using Npgsql;
using PgMini.Models;

namespace PgMini.Delegates;

/// <summary>
/// A step in a transactional pipeline. Receives the query and parameters forwarded
/// from the previous step's post-action, plus the active transaction.
/// Autonomous steps (built with <c>AutonomousTx*Step</c>) ignore the first two arguments.
/// </summary>
public delegate Task<object?> TxStep(
    string? query,
    List<QueryParams>? parameters,
    NpgsqlTransaction transaction
);

/// <summary>
/// Post-action run after a READ or SCALAR step. The <paramref name="result"/> is the
/// raw return value of the step (a <c>List&lt;T&gt;</c> for reads, a scalar for scalars).
/// Returns the query and parameters to forward to the next step, or <c>(null, null)</c>
/// to pass nothing.
/// </summary>
public delegate Task<(string? NextQuery, List<QueryParams>? NextParams)> PostAction(object result);

/// <summary>
/// Post-action run after an EXECUTE step. The <paramref name="affectedRows"/> is the
/// number of rows affected by the previous command.
/// Returns the query and parameters to forward to the next step.
/// </summary>
public delegate Task<(string? NextQuery, List<QueryParams>? NextParams)> PostExecuteAction(int affectedRows);
