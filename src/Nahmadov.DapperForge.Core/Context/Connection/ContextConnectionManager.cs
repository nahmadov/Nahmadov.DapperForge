using System.Data;

using Dapper;

using Nahmadov.DapperForge.Core.Common;
using Nahmadov.DapperForge.Core.Exceptions;

namespace Nahmadov.DapperForge.Core.Context.Connection;

/// <summary>
/// Manages creation and lifecycle of the underlying database connection.
/// </summary>
/// <remarks>
/// <para><b>Connection Management Strategies:</b></para>
/// <list type="bullet">
/// <item><b>Legacy Mode:</b> Single persistent connection (backward compatible)</item>
/// <item><b>Scope Mode:</b> Scoped connections that return to pool (recommended)</item>
/// </list>
/// <para><b>Thread Safety:</b> All methods are thread-safe via locking.</para>
/// </remarks>
internal sealed class ContextConnectionManager(
    DapperDbContextOptions options,
    Action<string> logInformation,
    Action<Exception, string?, string> logError) : IDisposable
{
    private readonly DapperDbContextOptions _options = options;
    private readonly object _connectionLock = new();
    private IDbConnection? _connection;
    private IDbTransaction? _activeTransaction;
    private readonly Action<string> _logInformation = logInformation;
    private readonly Action<Exception, string?, string> _logError = logError;

    /// <summary>
    /// Gets an open database connection, creating one if necessary.
    /// Handles broken connections automatically by recreating them.
    /// </summary>
    /// <remarks>
    /// <para><b>Connection State Handling:</b></para>
    /// <list type="bullet">
    /// <item><b>Open:</b> Returns existing connection</item>
    /// <item><b>Broken:</b> Disposes and recreates connection</item>
    /// <item><b>Closed:</b> Reopens existing connection</item>
    /// <item><b>Other:</b> Recreates connection</item>
    /// </list>
    /// <para><b>Thread Safety:</b> Uses locking to prevent race conditions.</para>
    /// </remarks>
    /// <exception cref="DapperConfigurationException">Thrown when ConnectionFactory is not configured.</exception>
    /// <exception cref="DapperConnectionException">Thrown when connection cannot be established.</exception>
    public IDbConnection GetOpenConnection()
    {
        lock (_connectionLock)
        {
            const int maxAttempts = 3;
            Exception? lastException = null;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    // Handle existing connection
                    if (_connection != null)
                    {
                        // Connection is healthy and open
                        if (_connection.State == ConnectionState.Open)
                            return _connection;

                        // Connection is broken - dispose and recreate
                        if (_connection.State == ConnectionState.Broken)
                        {
                            _logInformation("Connection is broken, disposing and recreating");
                            _connection.Dispose();
                            _connection = null;
                        }
                        // Connection is closed - try to reopen
                        else if (_connection.State == ConnectionState.Closed)
                        {
                            _logInformation("Connection is closed, attempting to reopen");
                            try
                            {
                                _connection.Open();
                                _logInformation("Connection reopened successfully");
                                return _connection;
                            }
                            catch (Exception ex)
                            {
                                _logError(ex, null, "Failed to reopen closed connection, will recreate");
                                _connection.Dispose();
                                _connection = null;
                            }
                        }
                        // Unexpected state - recreate
                        else
                        {
                            _logInformation($"Connection in unexpected state '{_connection.State}', recreating");
                            _connection.Dispose();
                            _connection = null;
                        }
                    }

                    // Create new connection if needed
                    if (_connection is null)
                    {
                        if (_options.ConnectionFactory is null)
                        {
                            throw new DapperConfigurationException(
                                "ConnectionFactory is not configured. Provide a connection factory in the options.");
                        }

                        _connection = _options.ConnectionFactory();

                        if (_connection is null)
                        {
                            const string msg = "ConnectionFactory returned null";
                            _logError(new InvalidOperationException(msg), null, msg);
                            throw new DapperConnectionException(
                                "ConnectionFactory returned null. Ensure the factory creates a valid connection.");
                        }

                        if (_connection.State != ConnectionState.Open)
                        {
                            _logInformation($"Opening database connection to {_connection.Database ?? "database"}");
                            _connection.Open();
                            _logInformation("Database connection opened successfully");
                        }
                    }

                    return _connection;
                }
                catch (DapperForgeException)
                {
                    throw;
                }
                catch (Exception ex) when (attempt < maxAttempts - 1 && IsTransientConnectionError(ex))
                {
                    lastException = ex;
                    _logInformation($"Transient connection error on attempt {attempt + 1}/{maxAttempts}, retrying...");

                    // Cleanup failed connection
                    if (_connection != null)
                    {
                        try { _connection.Dispose(); } catch { /* ignore */ }
                        _connection = null;
                    }

                    // Wait before retry with exponential backoff
                    var delay = 100 * (int)Math.Pow(2, attempt);
                    Thread.Sleep(delay);
                }
                catch (Exception ex)
                {
                    _logError(ex, null, "Failed to establish database connection");
                    throw new DapperConnectionException(
                        $"Failed to establish database connection: {ex.Message}", ex);
                }
            }

            // All attempts failed
            var finalException = lastException ?? new InvalidOperationException("Connection failed after all retry attempts");
            _logError(finalException, null, $"Failed to establish connection after {maxAttempts} attempts");
            throw new DapperConnectionException(
                $"Failed to establish database connection after {maxAttempts} attempts: {finalException.Message}",
                finalException);
        }
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

    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            var connection = GetOpenConnection();

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
            _logInformation("Health check: Connection is healthy");
            return true;
        }
        catch (Exception ex)
        {
            _logError(ex, null, "Health check failed");
            throw new DapperConnectionException($"Connection health check failed: {ex.Message}", ex);
        }
    }

    public Task EnsureConnectionHealthyAsync()
    {
        try
        {
            var connection = GetOpenConnection();

            if (connection.State == ConnectionState.Broken)
            {
                _logInformation("Connection is broken, attempting to reconnect");
                connection.Close();
                connection.Open();
            }
            else if (connection.State != ConnectionState.Open)
            {
                _logInformation("Connection is not open, opening connection");
                connection.Open();
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logError(ex, null, "Failed to ensure connection health");
            throw new DapperConnectionException($"Failed to ensure connection health: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates a new connection scope for explicit connection lifecycle management.
    /// Recommended over GetOpenConnection() for better connection pool utilization.
    /// </summary>
    /// <returns>A new connection scope that should be disposed when done.</returns>
    /// <remarks>
    /// <para><b>Benefits of Connection Scope:</b></para>
    /// <list type="bullet">
    /// <item>Connections are returned to pool immediately after use</item>
    /// <item>Prevents connection pool exhaustion in high-traffic scenarios</item>
    /// <item>Explicit scope-based lifetime management</item>
    /// <item>Automatic transaction tracking and warnings</item>
    /// </list>
    /// </remarks>
    public IConnectionScope CreateConnectionScope()
    {
        return new ConnectionScope(_options.ConnectionFactory!, _logInformation, _logError);
    }

    /// <summary>
    /// Registers an active transaction.
    /// Used to track transaction lifecycle and warn on improper disposal.
    /// </summary>
    internal void RegisterTransaction(IDbTransaction transaction)
    {
        lock (_connectionLock)
        {
            _activeTransaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            _logInformation("Transaction registered");
        }
    }

    /// <summary>
    /// Unregisters an active transaction.
    /// Called when transaction is committed or rolled back.
    /// </summary>
    internal void UnregisterTransaction()
    {
        lock (_connectionLock)
        {
            _activeTransaction = null;
            _logInformation("Transaction unregistered");
        }
    }

    /// <summary>
    /// Gets whether there is an active transaction.
    /// </summary>
    public bool HasActiveTransaction
    {
        get
        {
            lock (_connectionLock)
            {
                return _activeTransaction is not null;
            }
        }
    }

    /// <summary>
    /// Disposes the connection manager and underlying connection.
    /// Warns if an active transaction exists to prevent data loss.
    /// </summary>
    public void Dispose()
    {
        lock (_connectionLock)
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

            // Dispose connection
            if (_connection is not null)
            {
                try
                {
                    if (_connection.State == ConnectionState.Open ||
                        _connection.State == ConnectionState.Broken)
                    {
                        _connection.Close();
                        _logInformation("Connection closed during disposal");
                    }

                    _connection.Dispose();
                }
                catch (Exception ex)
                {
                    _logError(ex, null, "Error disposing connection");
                }
                finally
                {
                    _connection = null;
                }
            }
        }
    }
}
