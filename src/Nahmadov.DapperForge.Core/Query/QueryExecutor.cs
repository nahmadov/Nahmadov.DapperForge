using System.Data;

using Dapper;

using Nahmadov.DapperForge.Core.Common;
using Nahmadov.DapperForge.Core.Exceptions;
using Nahmadov.DapperForge.Core.Interfaces;

namespace Nahmadov.DapperForge.Core.Query;

/// <summary>
/// Executes database queries with retry logic, health checks, and error handling.
/// Decouples query execution from context management.
/// </summary>
internal sealed class QueryExecutor : IQueryExecutor
{
    private readonly IDbConnection _connection;
    private readonly DapperDbContextOptions _options;
    private readonly Action<string> _logSql;
    private readonly Action<string> _logInfo;
    private readonly Action<Exception, string?, string> _logError;
    private readonly Func<Task> _ensureHealthyAsync;

    public QueryExecutor(
        IDbConnection connection,
        DapperDbContextOptions options,
        Action<string> logSql,
        Action<string> logInfo,
        Action<Exception, string?, string> logError,
        Func<Task> ensureHealthyAsync)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logSql = logSql ?? throw new ArgumentNullException(nameof(logSql));
        _logInfo = logInfo ?? throw new ArgumentNullException(nameof(logInfo));
        _logError = logError ?? throw new ArgumentNullException(nameof(logError));
        _ensureHealthyAsync = ensureHealthyAsync ?? throw new ArgumentNullException(nameof(ensureHealthyAsync));
    }

    public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null)
    {
        _logSql(sql);
        return ExecuteWithRetryAsync(async () =>
        {
            if (_options.EnableConnectionHealthCheck && transaction is null)
                await _ensureHealthyAsync().ConfigureAwait(false);

            var connection = transaction?.Connection ?? _connection;
            var timeout = _options.CommandTimeoutSeconds;
            return await connection.QueryAsync<T>(sql, param, transaction, commandTimeout: timeout).ConfigureAwait(false);
        });
    }

    public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null)
    {
        _logSql(sql);
        return ExecuteWithRetryAsync(async () =>
        {
            if (_options.EnableConnectionHealthCheck && transaction is null)
                await _ensureHealthyAsync().ConfigureAwait(false);

            var connection = transaction?.Connection ?? _connection;
            var timeout = _options.CommandTimeoutSeconds;
            return await connection.QueryFirstOrDefaultAsync<T>(sql, param, transaction, commandTimeout: timeout).ConfigureAwait(false);
        });
    }

    public Task<IEnumerable<object>> QueryDynamicAsync(Type entityType, string sql, object? param = null, IDbTransaction? transaction = null)
    {
        _logSql(sql);
        return ExecuteWithRetryAsync(async () =>
        {
            if (_options.EnableConnectionHealthCheck && transaction is null)
                await _ensureHealthyAsync().ConfigureAwait(false);

            var connection = transaction?.Connection ?? _connection;
            var timeout = _options.CommandTimeoutSeconds;

            var results = await connection.QueryAsync(entityType, sql, param, transaction, commandTimeout: timeout).ConfigureAwait(false);
            return results.Cast<object>();
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
            if (_options.EnableConnectionHealthCheck && transaction is null)
                await _ensureHealthyAsync().ConfigureAwait(false);

            var connection = transaction?.Connection ?? _connection;
            var timeout = _options.CommandTimeoutSeconds;
            var results = await connection.QueryAsync(sql, types, objs => map(objs), param: parameters, transaction: transaction, splitOn: splitOn, commandTimeout: timeout).ConfigureAwait(false);
            return results.ToList();
        });
    }

    public Task<int> ExecuteAsync(string sql, object? param = null, IDbTransaction? transaction = null)
    {
        _logSql(sql);
        return ExecuteWithRetryAsync(async () =>
        {
            if (_options.EnableConnectionHealthCheck && transaction is null)
                await _ensureHealthyAsync().ConfigureAwait(false);

            var connection = transaction?.Connection ?? _connection;
            var timeout = _options.CommandTimeoutSeconds;
            return await connection.ExecuteAsync(sql, param, transaction, commandTimeout: timeout).ConfigureAwait(false);
        });
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
    private static bool IsTransientError(Exception ex)
    {
        // Check common transient error patterns
        var message = ex.Message.ToLowerInvariant();

        return ex is TimeoutException
            || message.Contains("timeout")
            || message.Contains("connection")
            || message.Contains("network")
            || message.Contains("deadlock")
            || message.Contains("transport-level error")
            || ex.InnerException is TimeoutException;
    }
}
