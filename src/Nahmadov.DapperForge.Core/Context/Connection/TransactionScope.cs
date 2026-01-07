using System.Data;

using Nahmadov.DapperForge.Core.Exceptions;

namespace Nahmadov.DapperForge.Core.Context.Connection;

/// <summary>
/// Scoped database transaction that ensures proper lifecycle management with automatic rollback.
/// </summary>
/// <remarks>
/// <para><b>Lifecycle:</b></para>
/// <list type="number">
/// <item>Transaction is created with specified isolation level</item>
/// <item>Operations execute within transaction</item>
/// <item>Complete() marks transaction as successful</item>
/// <item>Dispose() commits if Complete() was called, otherwise rolls back</item>
/// </list>
/// <para><b>Exception Safety:</b></para>
/// <list type="bullet">
/// <item>Rollback failures are logged but do not throw exceptions</item>
/// <item>Connection is forcefully closed if rollback fails</item>
/// <item>Orphaned transaction prevention through aggressive cleanup</item>
/// </list>
/// </remarks>
internal sealed class TransactionScope : ITransactionScope
{
    private readonly IDbConnection _connection;
    private readonly Action<string> _logInformation;
    private readonly Action<Exception, string?, string> _logError;
    private readonly Action _unregisterCallback;
    private IDbTransaction? _transaction;
    private bool _completed;
    private bool _disposed;
    private bool _manuallyHandled;

    public TransactionScope(
        IDbConnection connection,
        IsolationLevel isolationLevel,
        Action<string> logInformation,
        Action<Exception, string?, string> logError,
        Action unregisterCallback)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logInformation = logInformation ?? throw new ArgumentNullException(nameof(logInformation));
        _logError = logError ?? throw new ArgumentNullException(nameof(logError));
        _unregisterCallback = unregisterCallback ?? throw new ArgumentNullException(nameof(unregisterCallback));

        IsolationLevel = isolationLevel;

        try
        {
            _transaction = connection.BeginTransaction(isolationLevel);
            _logInformation($"Transaction started with isolation level: {isolationLevel}");
        }
        catch (Exception ex)
        {
            _logError(ex, null, "Failed to begin transaction");
            throw new DapperConnectionException($"Failed to begin transaction: {ex.Message}", ex);
        }
    }

    public IDbTransaction Transaction
    {
        get
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TransactionScope));

            if (_transaction is null)
                throw new InvalidOperationException("Transaction has been disposed");

            return _transaction;
        }
    }

    public IsolationLevel IsolationLevel { get; }

    public bool IsCompleted => _completed;

    /// <summary>
    /// Marks the transaction as complete.
    /// Transaction will be committed during Dispose().
    /// </summary>
    public void Complete()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TransactionScope));

        if (_manuallyHandled)
            throw new InvalidOperationException(
                "Cannot call Complete() after manual Commit() or Rollback()");

        _completed = true;
        _logInformation("Transaction marked as complete");
    }

    /// <summary>
    /// Manually commits the transaction immediately.
    /// </summary>
    public void Commit()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TransactionScope));

        if (_transaction is null)
            throw new InvalidOperationException("Transaction has been disposed");

        if (_manuallyHandled)
            throw new InvalidOperationException("Transaction has already been committed or rolled back");

        try
        {
            _transaction.Commit();
            _logInformation("Transaction committed manually");
            _manuallyHandled = true;
            _completed = true;
        }
        catch (Exception ex)
        {
            _logError(ex, null, "Failed to commit transaction");
            throw new DapperConnectionException($"Failed to commit transaction: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Manually rolls back the transaction immediately.
    /// </summary>
    public void Rollback()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TransactionScope));

        if (_transaction is null)
            throw new InvalidOperationException("Transaction has been disposed");

        if (_manuallyHandled)
            throw new InvalidOperationException("Transaction has already been committed or rolled back");

        try
        {
            _transaction.Rollback();
            _logInformation("Transaction rolled back manually");
            _manuallyHandled = true;
        }
        catch (Exception ex)
        {
            _logError(ex, null, "Failed to rollback transaction");
            HandleRollbackFailure(ex);
        }
    }

    /// <summary>
    /// Disposes the transaction scope.
    /// Commits if Complete() was called, otherwise rolls back.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            if (_transaction is not null && !_manuallyHandled)
            {
                if (_completed)
                {
                    // Complete() was called - commit transaction
                    try
                    {
                        _transaction.Commit();
                        _logInformation("Transaction committed on scope disposal");
                    }
                    catch (Exception ex)
                    {
                        _logError(ex, null, "Failed to commit transaction during disposal");

                        // Try to rollback after failed commit
                        TryRollbackAfterFailedCommit();

                        throw new DapperConnectionException(
                            $"Failed to commit transaction: {ex.Message}", ex);
                    }
                }
                else
                {
                    // Complete() was NOT called - rollback transaction
                    _logInformation("Transaction not completed - rolling back on disposal");

                    try
                    {
                        _transaction.Rollback();
                        _logInformation("Transaction rolled back successfully");
                    }
                    catch (Exception rollbackEx)
                    {
                        _logError(rollbackEx, null,
                            "Failed to rollback transaction during disposal - attempting cleanup");

                        HandleRollbackFailure(rollbackEx);
                    }
                }
            }

            // Dispose transaction
            if (_transaction is not null)
            {
                try
                {
                    _transaction.Dispose();
                }
                catch (Exception disposeEx)
                {
                    _logError(disposeEx, null, "Error disposing transaction");
                    // Swallow disposal exceptions
                }
                finally
                {
                    _transaction = null;
                }
            }

            // Unregister from connection manager
            _unregisterCallback();
        }
        catch (DapperForgeException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logError(ex, null, "Unexpected error during TransactionScope disposal");
            // Swallow exceptions during disposal to prevent masking original exceptions
        }
        finally
        {
            _disposed = true;
        }
    }

    /// <summary>
    /// Handles rollback failure by attempting aggressive cleanup.
    /// </summary>
    private void HandleRollbackFailure(Exception rollbackException)
    {
        try
        {
            _logError(rollbackException, null,
                "Rollback failed - attempting aggressive cleanup to prevent orphaned transaction");

            // Check connection state
            if (_connection.State == ConnectionState.Broken)
            {
                _logInformation("Connection is broken - closing to force transaction cleanup");
                _connection.Close();
                return;
            }

            // Try to force close connection to cleanup transaction
            if (_connection.State == ConnectionState.Open)
            {
                _logInformation("Forcing connection close to cleanup failed transaction");
                _connection.Close();
            }

            _logInformation("Aggressive cleanup completed");
        }
        catch (Exception cleanupEx)
        {
            _logError(cleanupEx, null,
                "Failed to perform aggressive cleanup - orphaned transaction may exist");

            // At this point, we've done everything we can
            // Log critical error for DBA attention
            _logError(new InvalidOperationException("CRITICAL: Potential orphaned transaction detected"),
                null,
                "Manual database cleanup may be required");
        }
    }

    /// <summary>
    /// Attempts to rollback after a failed commit.
    /// </summary>
    private void TryRollbackAfterFailedCommit()
    {
        try
        {
            _logInformation("Attempting rollback after failed commit");

            if (_transaction is not null)
            {
                _transaction.Rollback();
                _logInformation("Rollback after failed commit successful");
            }
        }
        catch (Exception rollbackEx)
        {
            _logError(rollbackEx, null, "Rollback after failed commit also failed");
            HandleRollbackFailure(rollbackEx);
        }
    }
}
