using System.Data;

namespace Nahmadov.DapperForge.Core.Abstractions;
/// <summary>
/// Provides core query execution capabilities with retry logic and health checks.
/// Decouples query execution from context management.
/// </summary>
internal interface IQueryExecutor
{
    /// <summary>
    /// Executes a query and returns all matching rows.
    /// Includes automatic retry logic for transient failures.
    /// </summary>
    /// <typeparam name="T">Type to map results to.</typeparam>
    /// <param name="sql">SQL to execute.</param>
    /// <param name="param">Optional parameters.</param>
    /// <param name="transaction">Optional transaction.</param>
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null);

    /// <summary>
    /// Executes a query and returns the first row or default.
    /// Includes automatic retry logic for transient failures.
    /// </summary>
    /// <typeparam name="T">Type to map results to.</typeparam>
    /// <param name="sql">SQL to execute.</param>
    /// <param name="param">Optional parameters.</param>
    /// <param name="transaction">Optional transaction.</param>
    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null);

    /// <summary>
    /// Executes a query with dynamic type resolution (non-generic).
    /// Used internally to avoid reflection overhead in Include operations.
    /// </summary>
    /// <param name="entityType">The entity type to query for.</param>
    /// <param name="sql">SQL to execute.</param>
    /// <param name="param">Optional parameters.</param>
    /// <param name="transaction">Optional transaction.</param>
    Task<IEnumerable<object>> QueryDynamicAsync(Type entityType, string sql, object? param = null, IDbTransaction? transaction = null);

    /// <summary>
    /// Executes a multi-mapping query using Dapper's Query with types array.
    /// </summary>
    /// <typeparam name="TEntity">Root entity type.</typeparam>
    /// <param name="sql">SQL to execute.</param>
    /// <param name="types">Array of types for multi-mapping.</param>
    /// <param name="parameters">Query parameters.</param>
    /// <param name="splitOn">Column names to split on.</param>
    /// <param name="map">Mapping function to convert object array to entity.</param>
    /// <param name="transaction">Optional transaction.</param>
    Task<List<TEntity?>> QueryWithTypesAsync<TEntity>(
        string sql,
        Type[] types,
        object parameters,
        string splitOn,
        Func<object?[], TEntity?> map,
        IDbTransaction? transaction = null);

    /// <summary>
    /// Executes a non-query command and returns affected row count.
    /// Includes automatic retry logic for transient failures.
    /// </summary>
    /// <param name="sql">SQL to execute.</param>
    /// <param name="param">Optional parameters.</param>
    /// <param name="transaction">Optional transaction.</param>
    Task<int> ExecuteAsync(string sql, object? param = null, IDbTransaction? transaction = null);
}

