using System.Data;

using Nahmadov.DapperForge.Core.Infrastructure.Exceptions;

namespace Nahmadov.DapperForge.Core.Context.Connection;
/// <summary>
/// Scoped database connection that ensures proper lifecycle management.
/// Automatically closes and returns connection to pool on disposal.
/// </summary>
/// <remarks>
/// <para><b>Lifecycle:</b></para>
/// <list type="number">
/// <item>Connection is created lazily on first access</item>
/// <item>Connection is opened if not already open</item>
/// <item>Connection health is verified before use</item>
/// <item>Connection is closed and returned to pool on Dispose()</item>
/// </list>
/// <para><b>Thread Safety:</b> Not thread-safe. Use one scope per logical operation.</para>
/// </remarks>
internal sealed class ConnectionScope(
    Func<IDbConnection> connectionFactory,
    Action<string> logInformation,
    Action<Exception, string?, string> logError) : IConnectionScope
{
    private readonly Func<IDbConnection> _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    private readonly Action<string> _logInformation = logInformation ?? throw new ArgumentNullException(nameof(logInformation));
    private readonly Action<Exception, string?, string> _logError = logError ?? throw new ArgumentNullException(nameof(logError));
    private IDbConnection? _connection;
    private IDbTransaction? _transaction;
    private bool _disposed;

    /// <summary>
    /// Gets the database connection, creating and opening it if necessary.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if scope is already disposed.</exception>
    /// <exception cref="DapperConnectionException">Thrown if connection cannot be established.</exception>
    public IDbConnection Connection
    {
        get
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ConnectionScope));
            }

            if (_connection is null)
            {
                _connection = CreateAndOpenConnection();
            }
            else
            {
                EnsureConnectionHealthy();
            }

            return _connection;
        }
    }

    public bool HasActiveTransaction => _transaction is not null;

    public IDbTransaction? ActiveTransaction => _transaction;

    /// <summary>
    /// Sets the active transaction for this scope.
    /// Used internally to track transaction lifecycle.
    /// </summary>
    internal void SetTransaction(IDbTransaction transaction)
    {
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
    }

    /// <summary>
    /// Clears the active transaction.
    /// Called when transaction is committed or rolled back.
    /// </summary>
    internal void ClearTransaction()
    {
        _transaction = null;
    }

    /// <summary>
    /// Creates and opens a new database connection.
    /// </summary>
    private IDbConnection CreateAndOpenConnection()
    {
        try
        {
            var connection = _connectionFactory();

            if (connection is null)
            {
                const string msg = "ConnectionFactory returned null";
                _logError(new InvalidOperationException(msg), null, msg);
                throw new DapperConnectionException(
                    "ConnectionFactory returned null. Ensure the factory creates a valid connection.");
            }

            if (connection.State != ConnectionState.Open)
            {
                _logInformation($"Opening database connection to {connection.Database ?? "database"}");
                connection.Open();
                _logInformation("Database connection opened successfully");
            }

            return connection;
        }
        catch (DapperForgeException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logError(ex, null, "Failed to establish database connection");
            throw new DapperConnectionException(
                $"Failed to establish database connection: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Ensures the connection is in a healthy state.
    /// Handles broken connections by recreating them.
    /// </summary>
    private void EnsureConnectionHealthy()
    {
        if (_connection is null)
            return;

        try
        {
            // Check if connection is broken
            if (_connection.State == ConnectionState.Broken)
            {
                _logInformation("Connection is broken, attempting to reconnect");
                _connection.Close();
                _connection.Dispose();
                _connection = CreateAndOpenConnection();
                return;
            }

            // Reopen if closed
            if (_connection.State == ConnectionState.Closed)
            {
                _logInformation("Connection is closed, reopening");
                _connection.Open();
                return;
            }

            // Connection is Open - verify it's still usable
            if (_connection.State != ConnectionState.Open)
            {
                _logInformation($"Connection state is {_connection.State}, recreating connection");
                _connection.Dispose();
                _connection = CreateAndOpenConnection();
            }
        }
        catch (Exception ex)
        {
            _logError(ex, null, "Failed to ensure connection health");
            throw new DapperConnectionException($"Failed to ensure connection health: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Disposes the connection scope, closing and returning the connection to the pool.
    /// If an active transaction exists, a warning is logged but the connection is still closed.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            // Warn if transaction is still active
            if (_transaction is not null)
            {
                var msg = "Disposing ConnectionScope with active transaction. " +
                          "Transaction should be committed or rolled back before scope disposal.";
                _logError(new InvalidOperationException(msg), null, msg);

                // Try to rollback the transaction to prevent data loss
                try
                {
                    _transaction.Rollback();
                    _logInformation("Active transaction rolled back during scope disposal");
                }
                catch (Exception rollbackEx)
                {
                    _logError(rollbackEx, null, "Failed to rollback transaction during scope disposal");
                }
                finally
                {
                    _transaction.Dispose();
                    _transaction = null;
                }
            }

            // Close and dispose connection (returns to pool)
            if (_connection is not null)
            {
                if (_connection.State == ConnectionState.Open ||
                    _connection.State == ConnectionState.Broken)
                {
                    _connection.Close(); // Returns to pool
                    _logInformation("Connection closed and returned to pool");
                }

                _connection.Dispose();
                _connection = null;
            }
        }
        catch (Exception ex)
        {
            _logError(ex, null, "Error during ConnectionScope disposal");
            // Swallow exception during disposal to prevent masking original exceptions
        }
        finally
        {
            _disposed = true;
        }
    }
}


