using System.Data;

using Dapper;

using Nahmadov.DapperForge.Core.Context.Options;
using Nahmadov.DapperForge.Core.Context.Connection;
using Nahmadov.DapperForge.Core.Infrastructure.Exceptions;
using Nahmadov.DapperForge.Core.Abstractions;

namespace Nahmadov.DapperForge.Core.Querying.Execution;
/// <summary>
/// Executes database queries with retry logic, automatic connection scoping, and error handling.
/// All database operations automatically acquire and release connections from pool.
/// </summary>
/// <remarks>
/// <para><b>Connection Management:</b></para>
/// <list type="bullet">
/// <item>Every query automatically creates a connection scope</item>
/// <item>Connections are immediately returned to pool after operation completes</item>
/// <item>Transaction-aware: operations within transaction reuse transaction's connection</item>
/// </list>
/// </remarks>
internal sealed class QueryExecutor : IQueryExecutor
{
    private readonly IInternalConnectionManager _connectionManager;
    private readonly DapperDbContextOptions _options;
    private readonly Action<string> _logSql;
    private readonly Action<string> _logInfo;
    private readonly Action<Exception, string?, string> _logError;

    public QueryExecutor(
        IInternalConnectionManager connectionManager,
        DapperDbContextOptions options,
        Action<string> logSql,
        Action<string> logInfo,
        Action<Exception, string?, string> logError)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logSql = logSql ?? throw new ArgumentNullException(nameof(logSql));
        _logInfo = logInfo ?? throw new ArgumentNullException(nameof(logInfo));
        _logError = logError ?? throw new ArgumentNullException(nameof(logError));
    }

    public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null)
    {
        _logSql(sql);
        return ExecuteWithRetryAsync(async () =>
        {
            return await _connectionManager.ExecuteWithConnectionAsync(async connection =>
            {
                var timeout = _options.CommandTimeoutSeconds;
                return await connection.QueryAsync<T>(sql, param, transaction, commandTimeout: timeout).ConfigureAwait(false);
            }, transaction).ConfigureAwait(false);
        });
    }

    public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null)
    {
        _logSql(sql);
        return ExecuteWithRetryAsync(async () =>
        {
            return await _connectionManager.ExecuteWithConnectionAsync(async connection =>
            {
                var timeout = _options.CommandTimeoutSeconds;
                return await connection.QueryFirstOrDefaultAsync<T>(sql, param, transaction, commandTimeout: timeout).ConfigureAwait(false);
            }, transaction).ConfigureAwait(false);
        });
    }

    public Task<IEnumerable<object>> QueryDynamicAsync(Type entityType, string sql, object? param = null, IDbTransaction? transaction = null)
    {
        _logSql(sql);
        return ExecuteWithRetryAsync(async () =>
        {
            return await _connectionManager.ExecuteWithConnectionAsync(async connection =>
            {
                var timeout = _options.CommandTimeoutSeconds;
                var results = await connection.QueryAsync(entityType, sql, param, transaction, commandTimeout: timeout).ConfigureAwait(false);
                return results.Cast<object>();
            }, transaction).ConfigureAwait(false);
        });
    }

    public Task<List<TEntity?>> QueryWithTypesAsync<TEntity>(
        string sql,
        Type[] types,
        object parameters,
        string splitOn,
        Func<object?[], TEntity?> map,
        IDbTransaction? transaction = null)
    {
        _logSql(sql);
        return ExecuteWithRetryAsync(async () =>
        {
            return await _connectionManager.ExecuteWithConnectionAsync(async connection =>
            {
                var timeout = _options.CommandTimeoutSeconds;
                var results = await connection.QueryAsync(sql, types, objs => map(objs), param: parameters, transaction: transaction, splitOn: splitOn, commandTimeout: timeout).ConfigureAwait(false);
                return results.ToList();
            }, transaction).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Executes a command (INSERT/UPDATE/DELETE) without retry logic.
    /// Uses automatic connection scoping for safe pool management.
    /// </summary>
    /// <remarks>
    /// <para><b>Why No Retry for Mutations:</b></para>
    /// <para>
    /// INSERT/UPDATE/DELETE operations are NOT idempotent. Retrying can cause:
    /// <list type="bullet">
    /// <item>Duplicate inserts (if first attempt succeeded but response was lost)</item>
    /// <item>Multiple updates (changing data twice)</item>
    /// <item>Data corruption</item>
    /// </list>
    /// </para>
    /// <para>
    /// If a mutation fails due to transient error (timeout, network), the application
    /// should decide whether to retry at a higher level (e.g., with idempotency tokens).
    /// </para>
    /// </remarks>
    public async Task<int> ExecuteAsync(string sql, object? param = null, IDbTransaction? transaction = null)
    {
        _logSql(sql);

        // No retry for mutations - they are NOT idempotent
        return await _connectionManager.ExecuteWithConnectionAsync(async connection =>
        {
            var timeout = _options.CommandTimeoutSeconds;
            return await connection.ExecuteAsync(sql, param, transaction, commandTimeout: timeout).ConfigureAwait(false);
        }, transaction).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a database operation with retry logic for transient failures.
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
    {
        var maxRetries = _options.MaxRetryCount;
        var baseDelay = _options.RetryDelayMilliseconds;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (Exception ex) when (IsTransientError(ex) && attempt < maxRetries)
            {
                var delay = baseDelay * (int)Math.Pow(2, attempt);
                _logInfo($"Transient error detected. Retrying in {delay}ms (attempt {attempt + 1}/{maxRetries})");

                await Task.Delay(delay).ConfigureAwait(false);
            }
        }

        // This should never be reached, but needed for compiler
        throw new InvalidOperationException("Retry logic failed unexpectedly");
    }

    /// <summary>
    /// Determines if an exception represents a transient database error that can be retried.
    /// </summary>
    /// <remarks>
    /// <para><b>Transient Errors (Safe to Retry):</b></para>
    /// <list type="bullet">
    /// <item>Timeouts (query execution, connection)</item>
    /// <item>Deadlocks (SQL Server: 1205, Oracle: ORA-00060)</item>
    /// <item>Network errors during query execution</item>
    /// <item>Transport-level errors</item>
    /// </list>
    /// <para><b>Non-Transient Errors (DO NOT Retry):</b></para>
    /// <list type="bullet">
    /// <item>Configuration errors (invalid server, database, credentials)</item>
    /// <item>Permission errors</item>
    /// <item>Constraint violations</item>
    /// <item>SQL syntax errors</item>
    /// </list>
    /// <para><b>Database-Specific Error Codes:</b></para>
    /// <list type="bullet">
    /// <item>SQL Server: -2 (timeout), 1205 (deadlock), 40197/40501/40613 (Azure transient)</item>
    /// <item>Oracle: ORA-00060 (deadlock), ORA-01013 (user requested cancel)</item>
    /// </list>
    /// </remarks>
    private static bool IsTransientError(Exception ex)
    {
        // TimeoutException is always transient
        if (ex is TimeoutException || ex.InnerException is TimeoutException)
            return true;

        var message = ex.Message;
        var messageLower = message.ToLowerInvariant();

        // Exclude configuration errors (these should NEVER be retried)
        if (IsConfigurationError(messageLower))
            return false;

        // Check for database-specific transient error codes
        if (IsSqlServerTransientError(ex, message))
            return true;

        if (IsOracleTransientError(message))
            return true;

        // Check for common transient patterns (after excluding config errors)
        return messageLower.Contains("timeout")
            || messageLower.Contains("deadlock")
            || messageLower.Contains("transport-level error")
            || (messageLower.Contains("connection") && messageLower.Contains("lost"));
    }

    /// <summary>
    /// Checks if the error is a configuration/setup error that should NOT be retried.
    /// </summary>
    private static bool IsConfigurationError(string messageLower)
    {
        return messageLower.Contains("login failed")
            || messageLower.Contains("invalid object name")
            || messageLower.Contains("invalid column name")
            || messageLower.Contains("cannot open database")
            || messageLower.Contains("does not exist")
            || messageLower.Contains("permission denied")
            || messageLower.Contains("access denied")
            || (messageLower.Contains("network-related") && messageLower.Contains("instance-specific")) // SQL Server: wrong server name
            || messageLower.Contains("syntax error");
    }

    /// <summary>
    /// Checks for SQL Server specific transient error codes.
    /// </summary>
    private static bool IsSqlServerTransientError(Exception ex, string message)
    {
        // Use reflection to check SqlException.Number if available (avoid hard dependency)
        var sqlExceptionType = ex.GetType();
        if (sqlExceptionType.Name != "SqlException")
            return false;

        var numberProperty = sqlExceptionType.GetProperty("Number");
        if (numberProperty is null)
            return false;

        var errorNumber = (int?)numberProperty.GetValue(ex);
        if (errorNumber is null)
            return false;

        // SQL Server transient error codes
        return errorNumber.Value switch
        {
            -2 => true,      // Timeout
            1205 => true,    // Deadlock victim
            40197 => true,   // Azure: Service error processing request
            40501 => true,   // Azure: Service currently busy
            40613 => true,   // Azure: Database unavailable
            49918 => true,   // Azure: Cannot process request
            49919 => true,   // Azure: Cannot process create/update request
            49920 => true,   // Azure: Cannot process request
            4221 => true,    // Login timeout expired
            _ => false
        };
    }

    /// <summary>
    /// Checks for Oracle specific transient error codes.
    /// </summary>
    private static bool IsOracleTransientError(string message)
    {
        return message.Contains("ORA-00060")  // Deadlock detected
            || message.Contains("ORA-01013")  // User requested cancel of current operation
            || message.Contains("ORA-00028"); // Session killed
    }
}



