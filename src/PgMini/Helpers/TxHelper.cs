using PgMini.Delegates;
using PgMini.Models;

namespace PgMini.Helpers;

/// <summary>
/// Static helpers for building post-actions in a transactional pipeline.
/// </summary>
public static class TxHelper
{
    private static readonly (string?, List<QueryParams>?) _noOp = (null, null);

    /// <summary>
    /// Returns a <see cref="PostAction"/> that passes nothing to the next step.
    /// Use this when the current step's result does not need to influence the next step's query.
    /// </summary>
    public static PostAction NoOp() =>
        _ => Task.FromResult<(string?, List<QueryParams>?)>(_noOp);

    /// <summary>
    /// Returns a <see cref="PostExecuteAction"/> that passes nothing to the next step.
    /// </summary>
    public static PostExecuteAction NoOpExecute() =>
        _ => Task.FromResult<(string?, List<QueryParams>?)>(_noOp);
}
