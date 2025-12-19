using System.Collections.Concurrent;
using System.Data;
using System.Reflection;

using Dapper;

using Nahmadov.DapperForge.Core.Builders;
using Nahmadov.DapperForge.Core.Common;
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
    private bool _disposed;
    private readonly ConcurrentDictionary<Type, object> _sets = new();
    private readonly Dictionary<Type, EntityMapping> _model = [];
    private readonly HashSet<Type> _registeredEntityTypes = new();
    private bool _modelBuilt;

    /// <summary>
    /// Initializes a new instance of <see cref="DapperDbContext"/> with the specified options.
    /// </summary>
    /// <param name="options">Configuration options for the context.</param>
    protected DapperDbContext(DapperDbContextOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (options.Dialect is null)
            throw new InvalidOperationException("Dialect is not configured. Use a database provider extension (UseSqlServer, UseOracle, etc.).");
        if (_options.ConnectionFactory is null)
            throw new InvalidOperationException("ConnectionFactory is not configured.");
    }

    /// <summary>
    /// Gets an open database connection, creating one if necessary.
    /// </summary>
    protected IDbConnection Connection
    {
        get
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
                throw new InvalidOperationException("ConnectionFactory is not configured.");
            _connection ??= _options.ConnectionFactory();
            if (_connection.State != ConnectionState.Open)
                _connection.Open();

            return _connection;
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
        var connection = transaction?.Connection ?? Connection;
        return connection.QueryAsync<T>(sql, param, transaction);
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
        var connection = transaction?.Connection ?? Connection;
        return connection.QueryFirstOrDefaultAsync<T>(sql, param, transaction);
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
        var connection = transaction?.Connection ?? Connection;
        return connection.ExecuteAsync(sql, param, transaction);
    }

    /// <summary>
    /// Begins a new transaction on the underlying connection.
    /// </summary>
    public Task<IDbTransaction> BeginTransactionAsync()
    {
        var transaction = Connection.BeginTransaction();
        return Task.FromResult(transaction);
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
    internal EntityMapping GetEntityMapping<TEntity>() where TEntity : class
    {
        EnsureModelBuilt();
        var type = typeof(TEntity);


        if (!_registeredEntityTypes.Contains(type))
        {
            throw new InvalidOperationException(
                $"Entity '{type.Name}' is not registered in this context. " +
                $"Declare it as a public DapperSet<{type.Name}> property on '{GetType().Name}'.");
        }

        if (_model.TryGetValue(type, out var mapping))
            return mapping;

        throw new InvalidOperationException(
                $"Mapping for entity '{type.Name}' was not built. Ensure model building includes this entity.");
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
    /// Writes executed SQL to the console and debug output for diagnostics.
    /// </summary>
    /// <param name="sql">SQL text to log.</param>
    private void LogSql(string sql)
    {
        if (!_options.EnableSqlLogging || string.IsNullOrWhiteSpace(sql))
            return;

        Console.WriteLine($"[Nahmadov.DapperForge SQL] {sql}");
    }
}
