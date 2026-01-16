using System.Data;

using Dapper;

using Nahmadov.DapperForge.Core.Context.Options;
using Nahmadov.DapperForge.Core.Infrastructure.Exceptions;

namespace Nahmadov.DapperForge.Core.Context.Connection;
/// <summary>
/// Manages automatic connection lifecycle with scope-based pattern.
/// All connections are automatically acquired from factory and returned to pool after use.
/// </summary>
/// <remarks>
/// <para><b>Connection Management Strategy:</b></para>
/// <list type="bullet">
/// <item><b>Automatic Scoping:</b> Every operation creates a connection scope internally</item>
/// <item><b>Pool-First:</b> Connections immediately return to pool after operation completes</item>
/// <item><b>Transaction-Aware:</b> Operations within transaction reuse transaction's connection</item>
/// <item><b>Thread-Safe:</b> Uses SemaphoreSlim for async-friendly concurrency control</item>
/// </list>
/// <para><b>Performance Benefits:</b></para>
/// <list type="bullet">
/// <item>No connection pool exhaustion (connections immediately released)</item>
/// <item>Async-friendly locking (no thread blocking)</item>
/// <item>Transaction connection reuse (no additional connections during transaction)</item>
/// </list>
/// </remarks>
internal sealed class ContextConnectionManager(
    DapperDbContextOptions options,
    Action<string> logInformation,
    Action<Exception, string?, string> logError) : IInternalConnectionManager
{
    private readonly DapperDbContextOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly Action<string> _logInformation = logInformation ?? throw new ArgumentNullException(nameof(logInformation));
    private readonly Action<Exception, string?, string> _logError = logError ?? throw new ArgumentNullException(nameof(logError));

    // Async-friendly semaphore instead of lock
    private readonly SemaphoreSlim _transactionSemaphore = new(1, 1);
    private IDbTransaction? _activeTransaction;
    private bool _disposed;

    /// <summary>
    /// Executes an operation with automatic connection scoping.
    /// Connection is acquired, used, and immediately returned to pool.
    /// </summary>
    public async Task<T> ExecuteWithConnectionAsync<T>(Func<IDbConnection, Task<T>> operation)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ContextConnectionManager));

        ArgumentNullException.ThrowIfNull(operation);

        // Check if we're inside a transaction - if so, reuse transaction's connection
        await _transactionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_activeTransaction is not null)
            {
                _logInformation("Reusing transaction connection for operation");
                var result = await operation(_activeTransaction.Connection!).ConfigureAwait(false);
                return result;
            }
        }
        finally
        {
            _transactionSemaphore.Release();
        }

        // No active transaction - create automatic scope
        using var scope = CreateConnectionScopeInternal();
        return await operation(scope.Connection).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an operation with optional transaction context.
    /// If transaction provided, uses its connection; otherwise creates scoped connection.
    /// </summary>
    public async Task<T> ExecuteWithConnectionAsync<T>(Func<IDbConnection, Task<T>> operation, IDbTransaction? transaction)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ContextConnectionManager));

        ArgumentNullException.ThrowIfNull(operation);

        // If transaction provided explicitly, use its connection
        if (transaction is not null)
        {
            return await operation(transaction.Connection!).ConfigureAwait(false);
        }

        // Otherwise use automatic scoping
        return await ExecuteWithConnectionAsync(operation).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates an explicit connection scope for advanced scenarios.
    /// Most client code should NOT use this directly; automatic scoping is preferred.
    /// </summary>
    public IConnectionScope CreateConnectionScope()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ContextConnectionManager));

        return CreateConnectionScopeInternal();
    }

    /// <summary>
    /// Internal method to create connection scope with retry logic.
    /// </summary>
    private ConnectionScope CreateConnectionScopeInternal()
    {
        if (_options.ConnectionFactory is null)
        {
            throw new DapperConfigurationException(
                "ConnectionFactory is not configured. Provide a connection factory in the options.");
        }

        const int maxAttempts = 3;
        Exception? lastException = null;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                var scope = new ConnectionScope(_options.ConnectionFactory, _logInformation, _logError);

                // Pre-validate connection by accessing it (lazy initialization)
                var connection = scope.Connection;
                if (connection.State == ConnectionState.Open)
                {
                    return scope;
                }

                // If connection not open, something went wrong
                scope.Dispose();
                throw new DapperConnectionException("Connection scope created but connection is not open");
            }
            catch (DapperForgeException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < maxAttempts - 1 && IsTransientConnectionError(ex))
            {
                lastException = ex;
                _logInformation($"Transient connection error on attempt {attempt + 1}/{maxAttempts}, retrying...");

                // Wait before retry with exponential backoff
                var delay = 100 * (int)Math.Pow(2, attempt);
                Thread.Sleep(delay);
            }
            catch (Exception ex)
            {
                _logError(ex, null, "Failed to create connection scope");
                throw new DapperConnectionException(
                    $"Failed to create connection scope: {ex.Message}", ex);
            }
        }

        // All attempts failed
        var finalException = lastException ?? new InvalidOperationException("Connection scope creation failed after all retry attempts");
        _logError(finalException, null, $"Failed to create connection scope after {maxAttempts} attempts");
        throw new DapperConnectionException(
            $"Failed to create connection scope after {maxAttempts} attempts: {finalException.Message}",
            finalException);
    }

    /// <summary>
    /// Determines if an exception represents a transient connection error that can be retried.
    /// </summary>
    private static bool IsTransientConnectionError(Exception ex)
    {
        var message = ex.Message.ToLowerInvariant();
        return ex is TimeoutException
            || message.Contains("timeout")
            || message.Contains("transport-level error")
            || message.Contains("connection was closed")
            || message.Contains("broken pipe")
            || message.Contains("network");
    }

    /// <summary>
    /// Performs health check on connection pool by creating a test connection.
    /// </summary>
    public async Task<bool> HealthCheckAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ContextConnectionManager));

        try
        {
            using var scope = CreateConnectionScopeInternal();
            var connection = scope.Connection;

            if (connection.State != ConnectionState.Open)
            {
                _logInformation("Health check: Connection is not open");
                return false;
            }

            var sql = _options.Dialect!.Name.ToLowerInvariant() switch
            {
                "oracle" => "SELECT 1 FROM DUAL",
                _ => "SELECT 1"
            };

            await connection.QueryFirstOrDefaultAsync<int>(sql, commandTimeout: 5).ConfigureAwait(false);
            _logInformation("Health check: Connection pool is healthy");
            return true;
        }
        catch (Exception ex)
        {
            _logError(ex, null, "Health check failed");
            throw new DapperConnectionException($"Connection health check failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Begins a new transaction scope with automatic connection management.
    /// </summary>
    public async Task<ITransactionScope> BeginTransactionScopeAsync(IsolationLevel isolationLevel)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ContextConnectionManager));

        await _transactionSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_activeTransaction is not null)
            {
                throw new InvalidOperationException(
                    "A transaction is already active on this context. " +
                    "Nested transactions are not supported. " +
                    "Complete or rollback the existing transaction before starting a new one.");
            }

            // Create connection scope for transaction
            // Important: This scope is NOT disposed until transaction completes
            var connectionScope = CreateConnectionScopeInternal();

            try
            {
                var scope = new TransactionScope(
                    connectionScope,
                    isolationLevel,
                    _logInformation,
                    _logError,
                    () =>
                    {
                        // Unregister callback
                        _transactionSemaphore.Wait();
                        try
                        {
                            _activeTransaction = null;
                            _logInformation("Transaction unregistered");
                        }
                        finally
                        {
                            _transactionSemaphore.Release();
                        }
                    });

                // Register transaction
                _activeTransaction = scope.Transaction;
                _logInformation($"Transaction registered with isolation level: {isolationLevel}");

                return scope;
            }
            catch
            {
                // If transaction creation fails, dispose the connection scope
                connectionScope.Dispose();
                throw;
            }
        }
        finally
        {
            _transactionSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets whether there is an active transaction.
    /// </summary>
    public bool HasActiveTransaction
    {
        get
        {
            _transactionSemaphore.Wait();
            try
            {
                return _activeTransaction is not null;
            }
            finally
            {
                _transactionSemaphore.Release();
            }
        }
    }

    /// <summary>
    /// Disposes the connection manager.
    /// Warns if an active transaction exists to prevent data loss.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _transactionSemaphore.Wait();
        try
        {
            // Warn if transaction is still active
            if (_activeTransaction is not null)
            {
                var msg = "Disposing ContextConnectionManager with active transaction. " +
                          "Transaction should be committed or rolled back before context disposal. " +
                          "Uncommitted data may be lost.";
                _logError(new InvalidOperationException(msg), null, msg);

                // Try to rollback to prevent partial commits
                try
                {
                    _activeTransaction.Rollback();
                    _logInformation("Active transaction rolled back during disposal");
                }
                catch (Exception rollbackEx)
                {
                    _logError(rollbackEx, null, "Failed to rollback transaction during disposal");
                }
                finally
                {
                    try { _activeTransaction.Dispose(); } catch { /* ignore */ }
                    _activeTransaction = null;
                }
            }
        }
        finally
        {
            _transactionSemaphore.Release();
            _transactionSemaphore.Dispose();
            _disposed = true;
        }
    }
}



