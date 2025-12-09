using System.Data;

namespace Nahmadov.DapperForge.Core.Interfaces;

/// <summary>
/// Defines the minimal database operations exposed by <see cref="Context.DapperDbContext"/>.
/// </summary>
internal interface IDapperDbContext
{
    /// <summary>
    /// Executes a query and returns all matching rows.
    /// </summary>
    /// <typeparam name="T">Type to map results to.</typeparam>
    /// <param name="sql">SQL to execute.</param>
    /// <param name="param">Optional parameters.</param>
    /// <param name="transaction">Optional transaction.</param>
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null);

    /// <summary>
    /// Executes a query and returns the first row or default.
    /// </summary>
    /// <typeparam name="T">Type to map results to.</typeparam>
    /// <param name="sql">SQL to execute.</param>
    /// <param name="param">Optional parameters.</param>
    /// <param name="transaction">Optional transaction.</param>
    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null);

    /// <summary>
    /// Executes a non-query command and returns the number of affected rows.
    /// </summary>
    /// <param name="sql">SQL to execute.</param>
    /// <param name="param">Optional parameters.</param>
    /// <param name="transaction">Optional transaction.</param>
    Task<int> ExecuteAsync(string sql, object? param = null, IDbTransaction? transaction = null);

    /// <summary>
    /// Begins a new database transaction on the underlying connection.
    /// </summary>
    Task<IDbTransaction> BeginTransactionAsync();
}
