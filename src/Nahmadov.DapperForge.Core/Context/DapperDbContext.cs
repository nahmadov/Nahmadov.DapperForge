using System.Collections.Concurrent;
using System.Data;
using System.Reflection;

using Microsoft.Extensions.Logging;

using Nahmadov.DapperForge.Core.Builders;
using Nahmadov.DapperForge.Core.Common;
using Nahmadov.DapperForge.Core.Context.Connection;
using Nahmadov.DapperForge.Core.Context.Utilities;
using Nahmadov.DapperForge.Core.Exceptions;
using Nahmadov.DapperForge.Core.Interfaces;
using Nahmadov.DapperForge.Core.Mapping;
using Nahmadov.DapperForge.Core.Query;

namespace Nahmadov.DapperForge.Core.Context;

/// <summary>
/// Base class providing Dapper-based data access with entity mapping support.
/// </summary>
public abstract class DapperDbContext : IDapperDbContext, IDisposable
{
    private readonly DapperDbContextOptions _options;
    private readonly IInternalConnectionManager _connectionManager;
    private readonly ContextModelManager _modelManager;
    private readonly SqlGeneratorProvider _sqlGeneratorProvider;
    private bool _disposed;
    private readonly ConcurrentDictionary<Type, object> _sets = new();
    private IQueryExecutor? _queryExecutor;
    private static readonly ConcurrentDictionary<Type, int> _contextInstanceCounts = new();
    private readonly Guid _instanceId = Guid.NewGuid();

    /// <summary>
    /// Initializes a new instance of <see cref="DapperDbContext"/> with the specified options.
    /// </summary>
    protected DapperDbContext(DapperDbContextOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (options.Dialect is null)
        {
            throw new DapperConfigurationException(
                "Dialect is not configured. Use a database provider extension (UseSqlServer, UseOracle, etc.).");
        }

        if (_options.ConnectionFactory is null)
        {
            throw new DapperConfigurationException(
                "ConnectionFactory is not configured. Provide a connection factory in the options.");
        }

        // Singleton detection
        DetectSingletonAntiPattern();

        _connectionManager = new ContextConnectionManager(_options, LogInformation, LogError);
        _modelManager = new ContextModelManager(_options, GetType(), OnModelCreating);
        _sqlGeneratorProvider = new SqlGeneratorProvider(_options.Dialect!, _modelManager);
    }

    private void DetectSingletonAntiPattern()
    {
        var contextType = GetType();
        var count = _contextInstanceCounts.AddOrUpdate(contextType, 1, (_, c) => c + 1);

        // If very few instances of this context type exist globally, it's likely a singleton
        if (count == 1)
        {
            // First instance - start monitoring
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(1)).ConfigureAwait(false);

                // After 1 minute, if still only 1-2 instances, warn about potential singleton
                if (_contextInstanceCounts.TryGetValue(contextType, out var currentCount) && currentCount <= 2)
                {
                    LogWarning(
                        $"Context '{contextType.Name}' has only {currentCount} instance(s) after 1 minute. " +
                        "This may indicate singleton lifetime registration. " +
                        "DapperDbContext should be registered as SCOPED, not SINGLETON. " +
                        "Singleton contexts can cause connection pool exhaustion and memory leaks.");
                }
            });
        }
    }

    private IQueryExecutor QueryExecutor
    {
        get
        {
            if (_queryExecutor is null)
            {
                _queryExecutor = new QueryExecutor(
                    _connectionManager,
                    _options,
                    LogSql,
                    LogInformation,
                    LogError);
            }
            return _queryExecutor;
        }
    }

    [Obsolete("Direct connection access is deprecated. Use QueryAsync/ExecuteAsync methods instead, which handle connection lifecycle automatically. For explicit control, use CreateConnectionScope().", false)]
    protected IDbConnection Connection
    {
        get
        {
            // For backward compatibility, create a scope and return connection
            // This is not ideal but prevents breaking existing code
            LogWarning("Direct Connection property access is deprecated. Consider using automatic scoping instead.");

            var scope = _connectionManager.CreateConnectionScope();
            return scope.Connection;
        }
    }

    #region Low-level Dapper wrappers

    /// <summary>
    /// Executes a query and returns all matching rows.
    /// </summary>
    public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null)
    {
        return QueryExecutor.QueryAsync<T>(sql, param, transaction);
    }

    /// <summary>
    /// Executes a query and returns the first row or default.
    /// </summary>
    public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null)
    {
        return QueryExecutor.QueryFirstOrDefaultAsync<T>(sql, param, transaction);
    }

    public Task<List<TEntity?>> QueryWithTypesAsync<TEntity>(string sql, Type[] types, object parameters, string splitOn, Func<object?[], TEntity?> map, IDbTransaction? transaction = null)
    {
        return QueryExecutor.QueryWithTypesAsync(sql, types, parameters, splitOn, map, transaction);
    }

    /// <summary>
    /// Executes a non-query command and returns affected row count.
    /// </summary>
    public Task<int> ExecuteAsync(string sql, object? param = null, IDbTransaction? transaction = null)
    {
        return QueryExecutor.ExecuteAsync(sql, param, transaction);
    }

    /// <summary>
    /// Creates a new connection scope for explicit connection lifecycle management.
    /// </summary>
    public IConnectionScope CreateConnectionScope()
    {
        return _connectionManager.CreateConnectionScope();
    }

    [Obsolete("Use BeginTransactionScopeAsync() instead for better resource management.", false)]
    public async Task<IDbTransaction> BeginTransactionAsync()
    {
        return await BeginTransactionAsync(IsolationLevel.ReadCommitted).ConfigureAwait(false);
    }

    [Obsolete("Use BeginTransactionScopeAsync(isolationLevel) instead for better resource management.", false)]
    public async Task<IDbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel)
    {
        LogWarning("BeginTransactionAsync() is deprecated. Use BeginTransactionScopeAsync() for better resource management.");

        // For backward compatibility, create transaction scope and return transaction
        // This is not ideal but prevents breaking existing code
        var scope = await _connectionManager.BeginTransactionScopeAsync(isolationLevel).ConfigureAwait(false);
        return scope.Transaction;
    }

    [Obsolete("Use TransactionScope.Complete() instead when using BeginTransactionScopeAsync().", false)]
    public void CommitTransaction(IDbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        try
        {
            transaction.Commit();
            LogInformation("Transaction committed (legacy method)");
        }
        catch (Exception ex)
        {
            LogError(ex, null, "Failed to commit transaction");
            throw;
        }
    }

    [Obsolete("Use TransactionScope.Rollback() instead when using BeginTransactionScopeAsync().", false)]
    public void RollbackTransaction(IDbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        try
        {
            transaction.Rollback();
            LogInformation("Transaction rolled back (legacy method)");
        }
        catch (Exception ex)
        {
            LogError(ex, null, "Failed to rollback transaction");
            throw;
        }
    }

    public bool HasActiveTransaction => _connectionManager.HasActiveTransaction;

    /// <summary>
    /// Begins a new transaction scope with automatic connection management and commit/rollback handling.
    /// </summary>
    public Task<ITransactionScope> BeginTransactionScopeAsync()
    {
        return BeginTransactionScopeAsync(IsolationLevel.ReadCommitted);
    }

    /// <summary>
    /// Begins a new transaction scope with automatic connection management and commit/rollback handling.
    /// </summary>
    public Task<ITransactionScope> BeginTransactionScopeAsync(IsolationLevel isolationLevel)
    {
        return _connectionManager.BeginTransactionScopeAsync(isolationLevel);
    }

    /// <summary>
    /// Executes a query with dynamic type resolution for internal use in Include operations.
    /// </summary>
    public Task<IEnumerable<object>> QueryDynamicAsync(Type entityType, string sql, object? param = null, IDbTransaction? transaction = null)
    {
        return QueryExecutor.QueryDynamicAsync(entityType, sql, param, transaction);
    }

    /// <summary>
    /// Performs a health check on the database connection pool.
    /// </summary>
    public async Task<bool> HealthCheckAsync()
        => await _connectionManager.HealthCheckAsync().ConfigureAwait(false);

    #endregion

    /// <summary>
    /// Gets a set for querying and saving instances of the specified entity type.
    /// </summary>
    public DapperSet<TEntity> Set<TEntity>() where TEntity : class
    {
        _modelManager.EnsureModelBuilt();

        var wasAdded = false;
        var set = (DapperSet<TEntity>)_sets.GetOrAdd(typeof(TEntity), _ =>
        {
            wasAdded = true;
            return CreateSet<TEntity>();
        });

        // Warn if too many entity types are cached (indicates singleton context anti-pattern)
        if (wasAdded && _sets.Count > 50)
        {
            LogWarning(
                $"DapperSet cache has grown to {_sets.Count} entity types. " +
                "This may indicate the context is being used as a singleton. " +
                "Consider using scoped lifetime instead to prevent unbounded memory growth.");
        }

        return set;
    }

    private DapperSet<TEntity> CreateSet<TEntity>() where TEntity : class
    {
        var mapping = _modelManager.GetEntityMapping<TEntity>();
        var generator = _sqlGeneratorProvider.GetGenerator<TEntity>();
        return new DapperSet<TEntity>(this, generator, mapping);
    }

    /// <summary>
    /// Allows derived contexts to configure entity mappings.
    /// </summary>
    protected virtual void OnModelCreating(DapperModelBuilder modelBuilder) { }

    internal EntityMapping GetEntityMapping<TEntity>() where TEntity : class
    {
        return _modelManager.GetEntityMapping<TEntity>();
    }

    internal EntityMapping GetEntityMapping(Type entityType)
    {
        return _modelManager.GetEntityMapping(entityType);
    }

    internal object GetSqlGenerator(Type entityType)
    {
        return _sqlGeneratorProvider.GetGenerator(entityType);
    }

    internal static string GetSelectAllSqlFromGenerator(object generator)
    {
        var prop = generator.GetType().GetProperty("SelectAllSql", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new DapperConfigurationException(
                $"Generator '{generator.GetType().Name}' has no SelectAllSql property. This is an internal error.");

        return (string)(prop.GetValue(generator)
            ?? throw new DapperConfigurationException(
                $"SelectAllSql is null on '{generator.GetType().Name}'. This is an internal error."));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _connectionManager.Dispose();

            // Decrement instance count for singleton detection
            var contextType = GetType();
            _contextInstanceCounts.AddOrUpdate(contextType, 0, (_, c) => Math.Max(0, c - 1));
        }

        _disposed = true;
    }

    private void LogSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return;

        // Use ILogger if configured (takes precedence)
        if (_options.Logger != null)
        {
            _options.Logger.LogDebug("Executing SQL: {Sql}", sql);
            return;
        }

        // Fallback to console logging if enabled
        if (_options.EnableSqlLogging)
        {
            Console.WriteLine($"[Nahmadov.DapperForge SQL] {sql}");
        }
    }

    private void LogError(Exception exception, string? sql, string message)
    {
        if (_options.Logger != null)
        {
            _options.Logger.LogError(exception, "{Message}. SQL: {Sql}", message, sql ?? "N/A");
        }
    }

    private void LogInformation(string message)
    {
        _options.Logger?.LogInformation("{Message}", message);
    }

    private void LogWarning(string message)
    {
        _options.Logger?.LogWarning("{Message}", message);
    }
}
