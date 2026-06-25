using PgMini.Delegates;

namespace PgMini.Models;

/// <summary>
/// A single step in a <see cref="Transaction"/> pipeline.
/// <para>
/// <b>Step execution order:</b>
/// <list type="number">
///   <item><description><see cref="Runnable"/> is invoked with the query/params forwarded from the previous step.</description></item>
///   <item><description>Exactly one post-action may be set; it receives the step result and returns the forwarded query/params for the next step.</description></item>
/// </list>
/// </para>
/// <para>Set only one of <see cref="PostRead"/>, <see cref="PostExecute"/>, or <see cref="PostExecuteScalar"/>.</para>
/// </summary>
public sealed class TransactionalQuery
{
    /// <summary>The database operation to run. Build with <c>IDbClient.Tx*Step()</c> helpers.</summary>
    public required TxStep Runnable { get; init; }

    /// <summary>Post-action for READ steps. Receives <c>List&lt;T&gt;</c> cast as <c>object</c>.</summary>
    public PostAction? PostRead { get; init; }

    /// <summary>Post-action for EXECUTE steps. Receives the affected row count.</summary>
    public PostExecuteAction? PostExecute { get; init; }

    /// <summary>Post-action for SCALAR steps. Receives the scalar result cast as <c>object</c>.</summary>
    public PostAction? PostExecuteScalar { get; init; }
}
