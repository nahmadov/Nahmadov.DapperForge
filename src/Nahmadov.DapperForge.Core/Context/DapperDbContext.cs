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
    private readonly ContextConnectionManager _connectionManager;
    private readonly ContextModelManager _modelManager;
    private readonly SqlGeneratorProvider _sqlGeneratorProvider;
    private bool _disposed;
    private readonly ConcurrentDictionary<Type, object> _sets = new();
    private IQueryExecutor? _queryExecutor;

    /// <summary>
    /// Initializes a new instance of <see cref="DapperDbContext"/> with the specified options.
    /// </summary>
    /// <param name="options">Configuration options for the context.</param>
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

        _connectionManager = new ContextConnectionManager(_options, LogInformation, LogError);
        _modelManager = new ContextModelManager(_options, GetType(), OnModelCreating);
        _sqlGeneratorProvider = new SqlGeneratorProvider(_options.Dialect!, _modelManager);
    }

    /// <summary>
    /// Gets the query executor instance for executing database operations with retry logic.
    /// </summary>
    private IQueryExecutor QueryExecutor
    {
        get
        {
            if (_queryExecutor is null)
            {
                _queryExecutor = new QueryExecutor(
                    Connection,
                    _options,
                    LogSql,
                    LogInformation,
                    LogError,
                    EnsureConnectionHealthyAsync);
            }
            return _queryExecutor;
        }
    }

    /// <summary>
    /// Gets an open database connection, creating one if necessary.
    /// Thread-safe: Multiple threads can safely access this property concurrently.
    /// </summary>
    /// <exception cref="DapperConnectionException">Thrown when connection cannot be established.</exception>
    protected IDbConnection Connection
    {
        get => _connectionManager.GetOpenConnection();
    }

    #region Low-level Dapper wrappers

    /// <summary>
    /// Executes a query and returns all matching rows.
    /// </summary>
    /// <typeparam name="T">Type to map results to.</typeparam>
    /// <param name="sql">SQL to execute.</param>
    /// <param name="param">Optional parameters.</param>
    /// <param name="transaction">Optional transaction.</param>
    public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null)
    {
        return QueryExecutor.QueryAsync<T>(sql, param, transaction);
    }

    /// <summary>
    /// Executes a query and returns the first row or default.
    /// </summary>
    /// <typeparam name="T">Type to map results to.</typeparam>
    /// <param name="sql">SQL to execute.</param>
    /// <param name="param">Optional parameters.</param>
    /// <param name="transaction">Optional transaction.</param>
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
    /// <param name="sql">SQL to execute.</param>
    /// <param name="param">Optional parameters.</param>
    /// <param name="transaction">Optional transaction.</param>
    public Task<int> ExecuteAsync(string sql, object? param = null, IDbTransaction? transaction = null)
    {
        return QueryExecutor.ExecuteAsync(sql, param, transaction);
    }

    /// <summary>
    /// Creates a new connection scope for explicit connection lifecycle management.
    /// </summary>
    /// <returns>A connection scope that should be disposed when operations are complete.</returns>
    /// <remarks>
    /// <para><b>Recommended Usage:</b></para>
    /// <code>
    /// // Good: Connection returned to pool after use
    /// using var scope = context.CreateConnectionScope();
    /// var users = await context.QueryAsync&lt;User&gt;("SELECT * FROM Users", connection: scope.Connection);
    /// // scope.Dispose() returns connection to pool
    ///
    /// // Alternative: Legacy mode (connection stays open)
    /// var users = await context.QueryAsync&lt;User&gt;("SELECT * FROM Users");
    /// // Connection stays open until context disposal
    /// </code>
    /// <para><b>Benefits:</b></para>
    /// <list type="bullet">
    /// <item>Prevents connection pool exhaustion in high-traffic scenarios</item>
    /// <item>Connections returned to pool immediately after scope disposal</item>
    /// <item>Better resource utilization</item>
    /// </list>
    /// </remarks>
    public IConnectionScope CreateConnectionScope()
    {
        return _connectionManager.CreateConnectionScope();
    }

    /// <summary>
    /// Begins a new transaction on the underlying connection with default isolation level (ReadCommitted).
    /// </summary>
    /// <returns>A database transaction.</returns>
    /// <remarks>
    /// <para><b>Important:</b> Transaction must be explicitly committed or rolled back before context disposal.</para>
    /// <para>If the transaction is not committed/rolled back, it will be automatically rolled back during disposal
    /// and a warning will be logged.</para>
    /// </remarks>
    public Task<IDbTransaction> BeginTransactionAsync()
    {
        return BeginTransactionAsync(IsolationLevel.ReadCommitted);
    }

    /// <summary>
    /// Begins a new transaction on the underlying connection with specified isolation level.
    /// </summary>
    /// <param name="isolationLevel">The isolation level for the transaction.</param>
    /// <returns>A database transaction.</returns>
    /// <remarks>
    /// <para><b>Important:</b> Transaction must be explicitly committed or rolled back before context disposal.</para>
    /// <para>If the transaction is not committed/rolled back, it will be automatically rolled back during disposal
    /// and a warning will be logged.</para>
    /// </remarks>
    public Task<IDbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel)
    {
        var transaction = Connection.BeginTransaction(isolationLevel);

        // Register transaction for tracking
        _connectionManager.RegisterTransaction(transaction);

        return Task.FromResult(transaction);
    }

    /// <summary>
    /// Commits a transaction and unregisters it from tracking.
    /// </summary>
    /// <param name="transaction">The transaction to commit.</param>
    public void CommitTransaction(IDbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        try
        {
            transaction.Commit();
            _connectionManager.UnregisterTransaction();
        }
        catch (Exception ex)
        {
            LogError(ex, null, "Failed to commit transaction");
            throw;
        }
    }

    /// <summary>
    /// Rolls back a transaction and unregisters it from tracking.
    /// </summary>
    /// <param name="transaction">The transaction to rollback.</param>
    public void RollbackTransaction(IDbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        try
        {
            transaction.Rollback();
            _connectionManager.UnregisterTransaction();
        }
        catch (Exception ex)
        {
            LogError(ex, null, "Failed to rollback transaction");
            throw;
        }
    }

    /// <summary>
    /// Gets whether there is an active transaction.
    /// </summary>
    public bool HasActiveTransaction => _connectionManager.HasActiveTransaction;

    /// <summary>
    /// Begins a new transaction scope with default isolation level (ReadCommitted).
    /// </summary>
    /// <returns>A transaction scope that manages commit/rollback automatically.</returns>
    /// <remarks>
    /// <para><b>Recommended Pattern:</b></para>
    /// <code>
    /// using var txScope = await context.BeginTransactionScopeAsync();
    /// try
    /// {
    ///     await context.ExecuteAsync("UPDATE ...", transaction: txScope.Transaction);
    ///     txScope.Complete(); // Mark as successful
    /// }
    /// // Dispose: Commits if Complete() was called, otherwise rolls back
    /// </code>
    /// <para><b>Benefits:</b></para>
    /// <list type="bullet">
    /// <item>Automatic rollback if Complete() not called (exception safety)</item>
    /// <item>Prevents orphaned transactions</item>
    /// <item>Graceful handling of rollback failures</item>
    /// </list>
    /// </remarks>
    public Task<ITransactionScope> BeginTransactionScopeAsync()
    {
        return BeginTransactionScopeAsync(IsolationLevel.ReadCommitted);
    }

    /// <summary>
    /// Begins a new transaction scope with specified isolation level.
    /// </summary>
    /// <param name="isolationLevel">The isolation level for the transaction.</param>
    /// <returns>A transaction scope that manages commit/rollback automatically.</returns>
    /// <remarks>
    /// <para><b>Transaction Scope Pattern:</b></para>
    /// <para>
    /// The scope ensures proper transaction lifecycle:
    /// - Complete() marks transaction as successful
    /// - Dispose() commits if Complete() was called, otherwise rolls back
    /// - Rollback failures are handled gracefully to prevent orphaned transactions
    /// </para>
    /// </remarks>
    public Task<ITransactionScope> BeginTransactionScopeAsync(IsolationLevel isolationLevel)
    {
        var scope = new TransactionScope(
            Connection,
            isolationLevel,
            LogInformation,
            LogError,
            () => _connectionManager.UnregisterTransaction());

        // Register transaction for tracking
        _connectionManager.RegisterTransaction(scope.Transaction);

        return Task.FromResult<ITransactionScope>(scope);
    }

    /// <summary>
    /// Executes a query with dynamic type resolution (non-generic).
    /// Used internally to avoid reflection overhead in Include operations.
    /// Performance: ~1200x faster than reflection-based generic method invocation.
    /// </summary>
    /// <param name="entityType">The entity type to query for.</param>
    /// <param name="sql">SQL to execute.</param>
    /// <param name="param">Optional parameters.</param>
    /// <param name="transaction">Optional transaction.</param>
    /// <returns>Results as IEnumerable of objects.</returns>
    public Task<IEnumerable<object>> QueryDynamicAsync(Type entityType, string sql, object? param = null, IDbTransaction? transaction = null)
    {
        return QueryExecutor.QueryDynamicAsync(entityType, sql, param, transaction);
    }

    /// <summary>
    /// Performs a health check on the database connection.
    /// </summary>
    /// <returns>True if connection is healthy, otherwise throws exception.</returns>
    /// <exception cref="DapperConnectionException">Thrown when health check fails.</exception>
    public async Task<bool> HealthCheckAsync()
        => await _connectionManager.HealthCheckAsync().ConfigureAwait(false);

    #endregion

    /// <summary>
    /// Ensures the connection is healthy before executing a command.
    /// </summary>
    private Task EnsureConnectionHealthyAsync()
    {
        return _connectionManager.EnsureConnectionHealthyAsync();
    }

    /// <summary>
    /// Gets a <see cref="DapperSet{TEntity}"/> for querying and saving instances of the given type.
    /// </summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <returns>A set for the entity type.</returns>
    public DapperSet<TEntity> Set<TEntity>() where TEntity : class
    {
        _modelManager.EnsureModelBuilt();
        return (DapperSet<TEntity>)_sets.GetOrAdd(typeof(TEntity), _ => CreateSet<TEntity>());
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
    /// <param name="modelBuilder">Model builder to configure.</param>
    protected virtual void OnModelCreating(DapperModelBuilder modelBuilder) { }

    /// <summary>
    /// Retrieves the mapping for a given entity type, building the model if necessary.
    /// </summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <returns>Entity mapping metadata.</returns>
    /// <exception cref="DapperConfigurationException">Thrown when entity is not registered or mapping not found.</exception>
    internal EntityMapping GetEntityMapping<TEntity>() where TEntity : class
    {
        return _modelManager.GetEntityMapping<TEntity>();
    }

    /// <summary>
    /// Retrieves the mapping for a given entity type, building the model if necessary.
    /// </summary>
    /// <param name="entityType">Entity type.</param>
    /// <returns>Entity mapping metadata.</returns>
    /// <exception cref="DapperConfigurationException">Thrown when entity is not registered or mapping not found.</exception>
    internal EntityMapping GetEntityMapping(Type entityType)
    {
        return _modelManager.GetEntityMapping(entityType);
    }

    internal object GetSqlGenerator(Type entityType)
    {
        return _sqlGeneratorProvider.GetGenerator(entityType);
    }

    /// <summary>
    /// Helper: reads SelectAllSql from SqlGenerator&lt;T&gt; instance.
    /// </summary>
    internal static string GetSelectAllSqlFromGenerator(object generator)
    {
        var prop = generator.GetType().GetProperty("SelectAllSql", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new DapperConfigurationException(
                $"Generator '{generator.GetType().Name}' has no SelectAllSql property. This is an internal error.");

        return (string)(prop.GetValue(generator)
            ?? throw new DapperConfigurationException(
                $"SelectAllSql is null on '{generator.GetType().Name}'. This is an internal error."));
    }

    /// <summary>
    /// Disposes the context and its underlying connection.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs the actual disposal logic.
    /// </summary>
    /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _connectionManager.Dispose();
        }

        _disposed = true;
    }

    /// <summary>
    /// Logs executed SQL using configured logger or console fallback.
    /// Supports structured logging when ILogger is configured.
    /// </summary>
    /// <param name="sql">SQL text to log.</param>
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

    /// <summary>
    /// Logs an error during database operations.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="sql">SQL that caused the error.</param>
    /// <param name="message">Additional context message.</param>
    private void LogError(Exception exception, string? sql, string message)
    {
        if (_options.Logger != null)
        {
            _options.Logger.LogError(exception, "{Message}. SQL: {Sql}", message, sql ?? "N/A");
        }
    }

    /// <summary>
    /// Logs connection-related events.
    /// </summary>
    /// <param name="message">Log message.</param>
    private void LogInformation(string message)
    {
        _options.Logger?.LogInformation("{Message}", message);
    }
}
