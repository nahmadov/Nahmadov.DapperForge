using System.Collections.Concurrent;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

using Dapper;

using DapperToolkit.Core.Builders;
using DapperToolkit.Core.Common;
using DapperToolkit.Core.Interfaces;
using DapperToolkit.Core.Mapping;

namespace DapperToolkit.Core.Context;

public abstract class DapperDbContext : IDapperDbContext, IDisposable
{
    private readonly DapperDbContextOptions _options;
    private IDbConnection? _connection;
    private bool _disposed;
    private readonly ConcurrentDictionary<Type, object> _sets = new();
    private readonly Dictionary<Type, EntityMapping> _model = [];
    private bool _modelBuilt;

    protected DapperDbContext(DapperDbContextOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (options.Dialect is null)
            throw new InvalidOperationException("Dialect is not configured. Use a database provider extension (UseSqlServer, UseOracle, etc.).");
        if (_options.ConnectionFactory is null)
            throw new InvalidOperationException("ConnectionFactory is not configured.");
    }

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

    public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null)
    {
        var connection = transaction?.Connection ?? Connection;
        return connection.QueryAsync<T>(sql, param, transaction);
    }

    public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null)
    {
        var connection = transaction?.Connection ?? Connection;
        return connection.QueryFirstOrDefaultAsync<T>(sql, param, transaction);
    }

    public Task<int> ExecuteAsync(string sql, object? param = null, IDbTransaction? transaction = null)
    {
        var connection = transaction?.Connection ?? Connection;
        return connection.ExecuteAsync(sql, param, transaction);
    }

    public Task<IDbTransaction> BeginTransactionAsync()
    {
        var transaction = Connection.BeginTransaction();
        return Task.FromResult(transaction);
    }

    #endregion

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

    protected virtual void OnModelCreating(DapperModelBuilder modelBuilder) { }

    private void EnsureModelBuilt()
    {
        if (_modelBuilt) return;

        var builder = new DapperModelBuilder(_options.Dialect!);

        // 1) Attribute-lərə əsaslanan ilkin mapping-ləri yığ
        InitializeMappingsFromAttributes(builder);

        // 2) İstifadəçinin OnModelCreating config-lərini tətbiq et
        OnModelCreating(builder);

        // 3) DbSet property adından table adı konvensiyası
        ApplyDbSetNameConvention(builder);

        // 4) Nəticə modelini yadda saxla
        foreach (var kvp in builder.Build())
        {
            _model[kvp.Key] = kvp.Value;
        }

        _modelBuilt = true;
    }

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

    private static void ApplyAttributeMapping(DapperModelBuilder builder, Type entityType)
    {
        var method = typeof(DapperDbContext)
            .GetMethod(nameof(ApplyAttributeMappingGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(entityType);

        method.Invoke(null, [builder]);
    }

    private static void ApplyAttributeMappingGeneric<TEntity>(DapperModelBuilder builder)
        where TEntity : class
    {
        var mapping = EntityMappingCache<TEntity>.Mapping;
        var entityBuilder = builder.Entity<TEntity>();

        entityBuilder.ToTable(mapping.TableName, mapping.Schema);

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

    private static Expression<Func<TEntity, object?>> BuildPropertyLambda<TEntity>(PropertyInfo property)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var access = Expression.Property(parameter, property);
        var convert = Expression.Convert(access, typeof(object));
        return Expression.Lambda<Func<TEntity, object?>>(convert, parameter);
    }

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
                    b.ToTable(prop.Name);
                }
            });
        }
    }

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
            _connection?.Dispose();
            _connection = null;
        }

        _disposed = true;
    }
}
