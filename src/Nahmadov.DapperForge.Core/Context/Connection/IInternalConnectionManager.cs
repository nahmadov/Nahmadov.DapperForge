using System.Data;

namespace Nahmadov.DapperForge.Core.Context.Connection;
/// <summary>
/// Internal interface for automatic connection management.
/// Handles connection lifecycle transparently for all database operations.
/// </summary>
internal interface IInternalConnectionManager : IDisposable
{
    /// <summary>
    /// Executes an operation with automatic connection scoping.
    /// Connection is acquired from factory, used, and immediately returned to pool.
    /// </summary>
    /// <typeparam name="T">Return type of the operation.</typeparam>
    /// <param name="operation">The database operation to execute.</param>
    /// <returns>Result of the operation.</returns>
    Task<T> ExecuteWithConnectionAsync<T>(Func<IDbConnection, Task<T>> operation);

    /// <summary>
    /// Executes an operation within an existing transaction context.
    /// If transaction exists, uses its connection; otherwise creates scoped connection.
    /// </summary>
    /// <typeparam name="T">Return type of the operation.</typeparam>
    /// <param name="operation">The database operation to execute.</param>
    /// <param name="transaction">Optional transaction to use.</param>
    /// <returns>Result of the operation.</returns>
    Task<T> ExecuteWithConnectionAsync<T>(Func<IDbConnection, Task<T>> operation, IDbTransaction? transaction);

    /// <summary>
    /// Creates an explicit connection scope for advanced scenarios.
    /// Client code rarely needs this; most operations use automatic scoping.
    /// </summary>
    IConnectionScope CreateConnectionScope();

    /// <summary>
    /// Begins a new transaction scope with automatic connection management.
    /// </summary>
    Task<ITransactionScope> BeginTransactionScopeAsync(IsolationLevel isolationLevel);

    /// <summary>
    /// Checks if there is an active transaction.
    /// </summary>
    bool HasActiveTransaction { get; }

    /// <summary>
    /// Performs health check on connection pool.
    /// </summary>
    Task<bool> HealthCheckAsync();
}

