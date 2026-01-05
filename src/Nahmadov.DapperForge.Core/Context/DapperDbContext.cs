using System.Collections.Concurrent;
using System.Data;
using System.Reflection;

using Dapper;

using Microsoft.Extensions.Logging;

using Nahmadov.DapperForge.Core.Builders;
using Nahmadov.DapperForge.Core.Common;
using Nahmadov.DapperForge.Core.Exceptions;
using Nahmadov.DapperForge.Core.Extensions;
using Nahmadov.DapperForge.Core.Interfaces;
using Nahmadov.DapperForge.Core.Mapping;

namespace Nahmadov.DapperForge.Core.Context;

/// <summary>
/// Base class providing Dapper-based data access with entity mapping support.
/// </summary>
public abstract class DapperDbContext : IDapperDbContext, IDisposable
{
    private readonly DapperDbContextOptions _options;
    private IDbConnection? _connection;
    private readonly object _connectionLock = new();  // Thread-safety for connection management
    private bool _disposed;
    private readonly ConcurrentDictionary<Type, object> _sets = new();
    private readonly Dictionary<Type, EntityMapping> _model = [];
    private readonly HashSet<Type> _registeredEntityTypes = new();
    private readonly ConcurrentDictionary<Type, object> _sqlGeneratorCache = new();
    private bool _modelBuilt;

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
    }

    /// <summary>
    /// Gets an open database connection, creating one if necessary.
    /// Thread-safe: Multiple threads can safely access this property concurrently.
    /// </summary>
    /// <exception cref="DapperConnectionException">Thrown when connection cannot be established.</exception>
    protected IDbConnection Connection
    {
        get
        {
            lock (_connectionLock)
            {
                if (_connection != null)
                {
                    if (_connection.State == ConnectionState.Open)
                        return _connection;

                    if (_connection.State == ConnectionState.Broken)
                    {
                        _connection.Dispose();
                        _connection = null;
                    }
                }

                if (_options.ConnectionFactory is null)
                {
                    throw new DapperConfigurationException(
                        "ConnectionFactory is not configured. Provide a connection factory in the options.");
                }

                try
                {
                    _connection ??= _options.ConnectionFactory();

                    if (_connection is null)
                    {
                        const string msg = "ConnectionFactory returned null";
                        LogError(new InvalidOperationException(msg), null, msg);
                        throw new DapperConnectionException(
                            "ConnectionFactory returned null. Ensure the factory creates a valid connection.");
                    }

                    if (_connection.State != ConnectionState.Open)
                    {
                        LogInformation($"Opening database connection to {_connection.Database ?? "database"}");
                        _connection.Open();
                        LogInformation("Database connection opened successfully");
                    }

                    return _connection;
                }
                catch (DapperForgeException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LogError(ex, null, "Failed to establish database connection");
                    throw new DapperConnectionException(
                        $"Failed to establish database connection: {ex.Message}", ex);
                }
            }
        }
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
        LogSql(sql);
        return ExecuteWithRetryAsync(async () =>
        {
            if (_options.EnableConnectionHealthCheck && transaction is null)
                await EnsureConnectionHealthyAsync();

            var connection = transaction?.Connection ?? Connection;
            var timeout = _options.CommandTimeoutSeconds;
            return await connection.QueryAsync<T>(sql, param, transaction, commandTimeout: timeout);
        });
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
        LogSql(sql);
        return ExecuteWithRetryAsync(async () =>
        {
            if (_options.EnableConnectionHealthCheck && transaction is null)
                await EnsureConnectionHealthyAsync();

            var connection = transaction?.Connection ?? Connection;
            var timeout = _options.CommandTimeoutSeconds;
            return await connection.QueryFirstOrDefaultAsync<T>(sql, param, transaction, commandTimeout: timeout);
        });
    }

    public Task<List<TEntity?>> QueryWithTypesAsync<TEntity>(string sql, Type[] types, object parameters, string splitOn, Func<object?[], TEntity?> map, IDbTransaction? transaction = null)
    {
        LogSql(sql);
        return ExecuteWithRetryAsync(async () =>
        {
            if (_options.EnableConnectionHealthCheck && transaction is null)
                await EnsureConnectionHealthyAsync();

            var connection = transaction?.Connection ?? Connection;
            var timeout = _options.CommandTimeoutSeconds;
            var results = await connection.QueryAsync(sql, types, objs => map(objs), param: parameters, transaction: transaction, splitOn: splitOn, commandTimeout: timeout);
            return results.ToList();
        });
    }

    /// <summary>
    /// Executes a non-query command and returns affected row count.
    /// </summary>
    /// <param name="sql">SQL to execute.</param>
    /// <param name="param">Optional parameters.</param>
    /// <param name="transaction">Optional transaction.</param>
    public Task<int> ExecuteAsync(string sql, object? param = null, IDbTransaction? transaction = null)
    {
        LogSql(sql);
        return ExecuteWithRetryAsync(async () =>
        {
            if (_options.EnableConnectionHealthCheck && transaction is null)
                await EnsureConnectionHealthyAsync();

            var connection = transaction?.Connection ?? Connection;
            var timeout = _options.CommandTimeoutSeconds;
            return await connection.ExecuteAsync(sql, param, transaction, commandTimeout: timeout);
        });
    }

    /// <summary>
    /// Begins a new transaction on the underlying connection.
    /// </summary>
    public Task<IDbTransaction> BeginTransactionAsync()
    {
        var transaction = Connection.BeginTransaction();
        return Task.FromResult(transaction);
    }

    /// <summary>
    /// Performs a health check on the database connection.
    /// </summary>
    /// <returns>True if connection is healthy, otherwise throws exception.</returns>
    /// <exception cref="DapperConnectionException">Thrown when health check fails.</exception>
    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            var connection = Connection;

            if (connection.State != ConnectionState.Open)
            {
                LogInformation("Health check: Connection is not open");
                return false;
            }

            // Execute a simple query to verify connection is responsive
            var sql = _options.Dialect!.Name.ToLowerInvariant() switch
            {
                "oracle" => "SELECT 1 FROM DUAL",
                _ => "SELECT 1"
            };

            await connection.QueryFirstOrDefaultAsync<int>(sql, commandTimeout: 5);
            LogInformation("Health check: Connection is healthy");
            return true;
        }
        catch (Exception ex)
        {
            LogError(ex, null, "Health check failed");
            throw new DapperConnectionException($"Connection health check failed: {ex.Message}", ex);
        }
    }

    #endregion

    #region Retry Logic

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
                return await operation();
            }
            catch (Exception ex) when (IsTransientError(ex) && attempt < maxRetries)
            {
                var delay = baseDelay * (int)Math.Pow(2, attempt);
                LogInformation($"Transient error detected. Retrying in {delay}ms (attempt {attempt + 1}/{maxRetries})");

                await Task.Delay(delay);
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

    /// <summary>
    /// Ensures the connection is healthy before executing a command.
    /// </summary>
    private Task EnsureConnectionHealthyAsync()
    {
        try
        {
            var connection = Connection;

            if (connection.State == ConnectionState.Broken)
            {
                LogInformation("Connection is broken, attempting to reconnect");
                connection.Close();
                connection.Open();
            }
            else if (connection.State != ConnectionState.Open)
            {
                LogInformation("Connection is not open, opening connection");
                connection.Open();
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            LogError(ex, null, "Failed to ensure connection health");
            throw new DapperConnectionException($"Failed to ensure connection health: {ex.Message}", ex);
        }
    }

    #endregion

    /// <summary>
    /// Gets a <see cref="DapperSet{TEntity}"/> for querying and saving instances of the given type.
    /// </summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <returns>A set for the entity type.</returns>
    public DapperSet<TEntity> Set<TEntity>() where TEntity : class
    {
        EnsureModelBuilt();
        return (DapperSet<TEntity>)_sets.GetOrAdd(typeof(TEntity), _ =>
        {
            var mapping = GetEntityMapping<TEntity>();

            var generator = new SqlGenerator<TEntity>(_options.Dialect!, mapping);
            return new DapperSet<TEntity>(this, generator, mapping);
        });
    }

    /// <summary>
    /// Allows derived contexts to configure entity mappings.
    /// </summary>
    /// <param name="modelBuilder">Model builder to configure.</param>
    protected virtual void OnModelCreating(DapperModelBuilder modelBuilder) { }

    /// <summary>
    /// Ensures the model metadata is built once before accessing entity sets.
    /// </summary>
    private void EnsureModelBuilt()
    {
        if (_modelBuilt) return;

        var builder = new DapperModelBuilder(_options.Dialect!, _options.Dialect?.DefaultSchema);

        InitializeMappingsFromAttributes(builder);
        OnModelCreating(builder);
        ApplyDbSetNameConvention(builder);

        var dbSetEntityTypes = GetType()
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(p => p.PropertyType.IsGenericType &&
                    p.PropertyType.GetGenericTypeDefinition() == typeof(DapperSet<>))
        .Select(p => p.PropertyType.GetGenericArguments()[0]);

        foreach (var t in dbSetEntityTypes)
            _registeredEntityTypes.Add(t);

        foreach (var kvp in builder.Build())
        {
            _model[kvp.Key] = kvp.Value;
        }

        foreach (var kv in _model) // Dictionary<Type, EntityMapping>
        {
            DapperTypeMapExtensions.SetPrefixInsensitiveMap(kv.Key);
        }

        _modelBuilt = true;
    }

    /// <summary>
    /// Ensures entity types referenced by DbSet<T> are registered with the model builder.
    /// </summary>
    /// <param name="builder">Model builder receiving the configuration.</param>
    private void InitializeMappingsFromAttributes(DapperModelBuilder builder)
    {
        var entityTypes = new HashSet<Type>();

        var dbSetTypes = GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p =>
                p.PropertyType.IsGenericType &&
                p.PropertyType.GetGenericTypeDefinition() == typeof(DapperSet<>))
            .Select(p => p.PropertyType.GetGenericArguments()[0]);

        foreach (var t in dbSetTypes)
            entityTypes.Add(t);

        foreach (var entityType in entityTypes)
        {
            builder.Entity(entityType);
        }
    }

    /// <summary>
    /// Applies a convention that maps entities to table names matching DbSet property names when not explicitly set.
    /// </summary>
    /// <param name="builder">Model builder to update.</param>
    private void ApplyDbSetNameConvention(DapperModelBuilder builder)
    {
        var props = GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p =>
                p.PropertyType.IsGenericType &&
                p.PropertyType.GetGenericTypeDefinition() == typeof(DapperSet<>));

        foreach (var prop in props)
        {
            var entityType = prop.PropertyType.GetGenericArguments()[0];

            builder.Entity(entityType, b =>
            {
                if (string.IsNullOrWhiteSpace(b.TableName))
                {
                    b.ToTable(prop.Name, builder.DefaultSchema);
                }
            });
        }
    }

    /// <summary>
    /// Retrieves the mapping for a given entity type, building the model if necessary.
    /// </summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <returns>Entity mapping metadata.</returns>
    /// <exception cref="DapperConfigurationException">Thrown when entity is not registered or mapping not found.</exception>
    internal EntityMapping GetEntityMapping<TEntity>() where TEntity : class
    {
        EnsureModelBuilt();
        var type = typeof(TEntity);

        if (!_registeredEntityTypes.Contains(type))
        {
            throw new DapperConfigurationException(
                type.Name,
                $"Entity is not registered in this context. " +
                $"Declare it as a public DapperSet<{type.Name}> property on '{GetType().Name}'.");
        }

        if (_model.TryGetValue(type, out var mapping))
            return mapping;

        throw new DapperConfigurationException(
            type.Name,
            "Mapping was not built. Ensure model building includes this entity.");
    }

    /// <summary>
    /// Retrieves the mapping for a given entity type, building the model if necessary.
    /// </summary>
    /// <param name="entityType">Entity type.</param>
    /// <returns>Entity mapping metadata.</returns>
    /// <exception cref="DapperConfigurationException">Thrown when entity is not registered or mapping not found.</exception>
    internal EntityMapping GetEntityMapping(Type entityType)
    {
        EnsureModelBuilt();

        if (!_registeredEntityTypes.Contains(entityType))
        {
            throw new DapperConfigurationException(
                entityType.Name,
                $"Entity is not registered in this context. " +
                $"Declare it as a public DapperSet<{entityType.Name}> property on '{GetType().Name}'.");
        }

        if (_model.TryGetValue(entityType, out var mapping))
            return mapping;

        throw new DapperConfigurationException(
            entityType.Name,
            "Mapping was not built. Ensure it is registered in the model.");
    }

    internal object GetSqlGenerator(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        EnsureModelBuilt();

        return _sqlGeneratorCache.GetOrAdd(entityType, t =>
        {
            var mapping = GetEntityMapping(t);

            var genType = typeof(SqlGenerator<>).MakeGenericType(t);
            return Activator.CreateInstance(genType, _options.Dialect!, mapping)
                ?? throw new DapperConfigurationException(
                    t.Name,
                    "Could not create SqlGenerator. This is likely an internal error.");
        });
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
            _connection?.Dispose();
            _connection = null;
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
