using System.Data;

namespace Nahmadov.DapperForge.Core.Context.Connection;

/// <summary>
/// Represents a scoped database transaction that ensures proper transaction lifecycle management.
/// Transactions are automatically rolled back if not explicitly completed.
/// </summary>
/// <remarks>
/// <para><b>Problem Solved:</b></para>
/// <list type="bullet">
/// <item>Prevents orphaned transactions by ensuring rollback on disposal</item>
/// <item>Protects against transaction leaks in error scenarios</item>
/// <item>Provides explicit transaction completion semantics</item>
/// <item>Handles rollback failures gracefully</item>
/// </list>
/// <para><b>Usage Pattern:</b></para>
/// <code>
/// using var txScope = await context.BeginTransactionScopeAsync();
/// try
/// {
///     await context.ExecuteAsync("UPDATE ...", transaction: txScope.Transaction);
///     txScope.Complete(); // Mark as successful
/// }
/// // Dispose: Auto-commits if Complete() was called, otherwise rolls back
/// </code>
/// </remarks>
public interface ITransactionScope : IDisposable
{
    /// <summary>
    /// Gets the underlying database transaction.
    /// </summary>
    IDbTransaction Transaction { get; }

    /// <summary>
    /// Gets the isolation level of this transaction.
    /// </summary>
    IsolationLevel IsolationLevel { get; }

    /// <summary>
    /// Gets whether this transaction has been marked as complete.
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// Marks the transaction as complete, indicating it should be committed on disposal.
    /// If not called, the transaction will be rolled back on disposal.
    /// </summary>
    /// <remarks>
    /// <para><b>Important:</b> This method does NOT commit the transaction immediately.
    /// Commit happens during Dispose(). This design allows for exception safety.</para>
    /// </remarks>
    void Complete();

    /// <summary>
    /// Manually commits the transaction.
    /// Use this only if you need immediate commit without waiting for disposal.
    /// </summary>
    void Commit();

    /// <summary>
    /// Manually rolls back the transaction.
    /// Use this only if you need immediate rollback without waiting for disposal.
    /// </summary>
    void Rollback();
}
