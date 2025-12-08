using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

using Dapper;

using DapperToolkit.Core.Builders;
using DapperToolkit.Core.Common;
using DapperToolkit.Core.Interfaces;
using DapperToolkit.Core.Mapping;

namespace DapperToolkit.Core.Context;

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

        foreach (var kvp in builder.Build())
        {
            _model[kvp.Key] = kvp.Value;
        }

        _modelBuilt = true;
    }

    /// <summary>
    /// Initializes mappings based on attributes discovered on DbSet entity types.
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
            ApplyAttributeMapping(builder, entityType);
        }
    }

    /// <summary>
    /// Invokes the generic attribute-mapping routine for a runtime entity type.
    /// </summary>
    /// <param name="builder">Model builder.</param>
    /// <param name="entityType">Entity CLR type.</param>
    private static void ApplyAttributeMapping(DapperModelBuilder builder, Type entityType)
    {
        var method = typeof(DapperDbContext)
            .GetMethod(nameof(ApplyAttributeMappingGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(entityType);

        method.Invoke(null, [builder]);
    }

    /// <summary>
    /// Applies attribute-driven mapping configuration for a specific entity type.
    /// </summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <param name="builder">Model builder to update.</param>
    private static void ApplyAttributeMappingGeneric<TEntity>(DapperModelBuilder builder)
        where TEntity : class
    {
        var mapping = EntityMappingCache<TEntity>.Mapping;
        var entityBuilder = builder.Entity<TEntity>();

        var tableAttr = typeof(TEntity).GetCustomAttribute<TableAttribute>();
        if (tableAttr is not null || !string.IsNullOrWhiteSpace(mapping.Schema))
        {
            entityBuilder.ToTable(mapping.TableName, mapping.Schema);
        }

        if (mapping.KeyProperties.Count == 0)
        {
            entityBuilder.HasNoKey();
        }
        else
        {
            var keyExpressions = mapping.KeyProperties
                .Select(BuildPropertyLambda<TEntity>)
                .ToArray();
            entityBuilder.HasKey(keyExpressions);
        }

        foreach (var pm in mapping.PropertyMappings)
        {
            var pb = entityBuilder.Property(BuildPropertyLambda<TEntity>(pm.Property));
            if (!string.Equals(pm.ColumnName, pm.Property.Name, StringComparison.Ordinal))
            {
                pb.HasColumnName(pm.ColumnName);
            }
            if (pm.IsRequired)
            {
                pb.IsRequired();
            }
            if (pm.MaxLength is not null)
            {
                pb.HasMaxLength(pm.MaxLength.Value);
            }
            if (pm.IsReadOnly)
            {
                pb.IsReadOnly();
            }
        }
    }

    /// <summary>
    /// Builds a lambda expression to access a property for mapping configuration.
    /// </summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <param name="property">Property to access.</param>
    /// <returns>Expression selecting the property.</returns>
    private static Expression<Func<TEntity, object?>> BuildPropertyLambda<TEntity>(PropertyInfo property)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var access = Expression.Property(parameter, property);
        var convert = Expression.Convert(access, typeof(object));
        return Expression.Lambda<Func<TEntity, object?>>(convert, parameter);
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
        var type = typeof(TEntity);
        EnsureModelBuilt();
        if (_model.TryGetValue(type, out var mapping))
            return mapping;

        var fallback = EntityMappingCache<TEntity>.Mapping;
        _model[type] = fallback;
        return fallback;
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
    /// Writes executed SQL to the console for diagnostics.
    /// </summary>
    /// <param name="sql">SQL text to log.</param>
    private static void LogSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return;
        Console.WriteLine($"[DapperToolkit SQL] {sql}");
    }
}
