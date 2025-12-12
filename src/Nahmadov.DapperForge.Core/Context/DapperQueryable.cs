using System.Linq.Expressions;
using System.Reflection;

using Nahmadov.DapperForge.Core.Builders;
using Nahmadov.DapperForge.Core.Interfaces;
using Nahmadov.DapperForge.Core.Mapping;

namespace Nahmadov.DapperForge.Core.Context;

/// <summary>
/// Fluent query builder for DapperForge queries, similar to IQueryable in Entity Framework.
/// Allows chaining of Where, OrderBy, Skip, Take operations before execution.
/// </summary>
/// <typeparam name="TEntity">Type of entity being queried.</typeparam>
internal sealed class DapperQueryable<TEntity> : IDapperQueryable<TEntity> where TEntity : class
{
    private readonly DapperDbContext _context;
    private readonly SqlGenerator<TEntity> _generator;
    private readonly EntityMapping _mapping;
    private Expression<Func<TEntity, bool>>? _predicate;
    private Expression<Func<TEntity, object?>>? _orderBy;
    private bool _isOrderByDescending;
    private int _skipCount;
    private int _takeCount;
    private bool _ignoreCase;
    private readonly List<(Type RelatedType, PropertyInfo NavigationProperty)> _includes = new();

    /// <summary>
    /// Initializes a new query builder for the given context.
    /// </summary>
    internal DapperQueryable(DapperDbContext context, SqlGenerator<TEntity> generator, EntityMapping mapping)
    {
        _context = context;
        _generator = generator;
        _mapping = mapping;
        _skipCount = 0;
        _takeCount = int.MaxValue;
    }

    public IDapperQueryable<TEntity> Where(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _predicate = predicate;
        _ignoreCase = ignoreCase;
        return this;
    }

    public IDapperQueryable<TEntity> OrderBy(Expression<Func<TEntity, object?>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        _orderBy = keySelector;
        _isOrderByDescending = false;
        return this;
    }

    public IDapperQueryable<TEntity> OrderByDescending(Expression<Func<TEntity, object?>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        _orderBy = keySelector;
        _isOrderByDescending = true;
        return this;
    }

    public IDapperQueryable<TEntity> Skip(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Skip count cannot be negative.");
        _skipCount = count;
        return this;
    }

    public IDapperQueryable<TEntity> Take(int count)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "Take count must be 1 or greater.");
        _takeCount = count;
        return this;
    }

    public IDapperQueryable<TEntity> Include<TRelated>(Expression<Func<TEntity, TRelated?>> navigationSelector) where TRelated : class
    {
        ArgumentNullException.ThrowIfNull(navigationSelector);
        if (navigationSelector.Body is MemberExpression member)
        {
            if (member.Member is PropertyInfo prop)
            {
                _includes.Add((typeof(TRelated), prop));
            }
        }
        return this;
    }

    public IDapperQueryable<TEntity> Include<TRelated>(Expression<Func<TEntity, IEnumerable<TRelated>>> navigationSelector) where TRelated : class
    {
        ArgumentNullException.ThrowIfNull(navigationSelector);
        if (navigationSelector.Body is MemberExpression member)
        {
            if (member.Member is PropertyInfo prop)
            {
                _includes.Add((typeof(TRelated), prop));
            }
        }
        return this;
    }

    public async Task<IEnumerable<TEntity>> ToListAsync()
    {
        var sql = BuildSql();
        var parameters = GetParameters();
        var results = await _context.QueryAsync<TEntity>(sql, parameters);
        var resultList = results.ToList();

        // Load related entities if includes are specified
        if (_includes.Count > 0)
        {
            await LoadIncludedEntitiesAsync(resultList);
        }

        return resultList;
    }

    private async Task LoadIncludedEntitiesAsync(List<TEntity> results)
    {
        if (results.Count == 0)
            return;

        foreach (var (relatedType, navProp) in _includes)
        {
            // Get the FK property that references this related entity
            var fkMapping = _mapping.ForeignKeys.FirstOrDefault(fk => fk.NavigationProperty == navProp);
            if (fkMapping is null)
                continue;

            // Get all the FK values from the main results
            var fkProperty = _mapping.PropertyMappings.FirstOrDefault(pm => pm.ColumnName == fkMapping.ForeignKeyColumnName)?.Property;
            if (fkProperty is null)
                continue;

            var fkValues = results
                .Select(r => fkProperty.GetValue(r))
                .Where(v => v is not null)
                .Distinct()
                .ToList();

            if (fkValues.Count == 0)
                continue;

            // Load related entities
            var relatedEntities = await LoadRelatedEntitiesAsync(relatedType, fkMapping, fkValues);

            // Populate navigation properties
            PopulateNavigationProperties(results, navProp, fkProperty, relatedEntities, fkMapping);
        }
    }

    private async Task<Dictionary<object, object>> LoadRelatedEntitiesAsync(
        Type relatedType,
        ForeignKeyMapping fkMapping,
        List<object?> fkValues)
    {
        // Build a query method dynamically using reflection
        var queryMethod = _context.GetType()
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "QueryAsync" && m.IsGenericMethodDefinition);

        if (queryMethod is null)
            return new Dictionary<object, object>();

        var genericQueryMethod = queryMethod.MakeGenericMethod(relatedType);

        // Build IN clause for FK values
        var fkColumnName = fkMapping.ForeignKeyColumnName;
        var tableName = fkMapping.PrincipalTableName;
        var schema = fkMapping.PrincipalSchema;
        var fullTableName = schema is not null ? $"[{schema}].[{tableName}]" : $"[{tableName}]";

        var sql = $"SELECT * FROM {fullTableName} WHERE [{fkColumnName}] IN @fkValues";
        var parameters = new Dictionary<string, object> { { "fkValues", fkValues } };

        var task = (Task)genericQueryMethod.Invoke(_context, new object[] { sql, parameters })!;
        await task.ConfigureAwait(false);

        var resultProperty = task.GetType().GetProperty("Result");
        var enumerable = resultProperty?.GetValue(task) as System.Collections.IEnumerable;

        var result = new Dictionary<object, object>();
        var relatedKeyProp = relatedType.GetProperty(fkMapping.PrincipalKeyColumnName);

        if (enumerable is not null && relatedKeyProp is not null)
        {
            foreach (var entity in enumerable)
            {
                var keyValue = relatedKeyProp.GetValue(entity);
                if (keyValue is not null)
                {
                    result[keyValue] = entity;
                }
            }
        }

        return result;
    }

    private void PopulateNavigationProperties(
        List<TEntity> results,
        PropertyInfo navigationProperty,
        PropertyInfo fkProperty,
        Dictionary<object, object> relatedEntities,
        ForeignKeyMapping fkMapping)
    {
        if (!relatedEntities.Any())
            return;

        var isCollection = navigationProperty.PropertyType.IsGenericType &&
                          navigationProperty.PropertyType.GetGenericTypeDefinition() == typeof(List<>);

        foreach (var result in results)
        {
            var fkValue = fkProperty.GetValue(result);
            if (fkValue is null)
                continue;

            if (relatedEntities.TryGetValue(fkValue, out var relatedEntity))
            {
                if (isCollection)
                {
                    var listType = navigationProperty.PropertyType;
                    var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
                    list.Add(relatedEntity);
                    navigationProperty.SetValue(result, list);
                }
                else
                {
                    navigationProperty.SetValue(result, relatedEntity);
                }
            }
        }
    }

    public async Task<TEntity?> FirstOrDefaultAsync()
    {
        var sql = BuildSql();
        var parameters = GetParameters();
        return await _context.QueryFirstOrDefaultAsync<TEntity>(sql, parameters);
    }

    public async Task<TEntity?> SingleOrDefaultAsync()
    {
        var sql = BuildSql();
        var parameters = GetParameters();
        var results = await _context.QueryAsync<TEntity>(sql, parameters);
        var list = results.ToList();

        if (list.Count > 1)
        {
            throw new InvalidOperationException(
                $"SingleOrDefaultAsync expected 0 or 1 result(s), but found {list.Count}.");
        }

        return list.FirstOrDefault();
    }

    public async Task<long> CountAsync()
    {
        var baseSql = $"SELECT COUNT(*) FROM {_generator.TableName} AS a";
        var parameters = GetParameters();

        if (_predicate is not null)
        {
            var dialect = _generator.Dialect;
            var visitor = new PredicateVisitor<TEntity>(_mapping, dialect);
            var (whereClause, whereParams) = visitor.Translate(_predicate, _ignoreCase);
            baseSql = $"{baseSql} WHERE {whereClause}";
            parameters = whereParams;
        }

        return await _context.QueryFirstOrDefaultAsync<long>(baseSql, parameters);
    }

    private string BuildSql()
    {
        var sql = _generator.SelectAllSql;

        // Apply WHERE
        if (_predicate is not null)
        {
            var dialect = _generator.Dialect;
            var visitor = new PredicateVisitor<TEntity>(_mapping, dialect);
            var (whereClause, _) = visitor.Translate(_predicate, _ignoreCase);
            sql = $"{sql} WHERE {whereClause}";
        }

        // Apply ORDER BY
        if (_orderBy is not null)
        {
            var dialect = _generator.Dialect;
            var visitor = new OrderingVisitor<TEntity>(_mapping, dialect);
            var orderClause = visitor.Translate(_orderBy, _isOrderByDescending);
            if (!string.IsNullOrEmpty(orderClause))
            {
                sql = $"{sql} ORDER BY {orderClause}";
            }
        }
        else if (_skipCount > 0 || _takeCount < int.MaxValue)
        {
            // Default ordering by first key property when pagination is used without explicit order
            if (_mapping.KeyProperties.Count > 0)
            {
                var keyProp = _mapping.KeyProperties[0];
                var keyMapping = _mapping.PropertyMappings.First(pm => pm.Property == keyProp);
                var orderClause = $"a.{_generator.Dialect.QuoteIdentifier(keyMapping.ColumnName)}";
                sql = $"{sql} ORDER BY {orderClause}";
            }
        }

        // Apply SKIP/TAKE (pagination)
        if (_skipCount > 0 || _takeCount < int.MaxValue)
        {
            sql = BuildPaginatedSql(sql, _skipCount, _takeCount);
        }

        return sql;
    }

    private object GetParameters()
    {
        if (_predicate is null)
        {
            return new Dictionary<string, object?>();
        }

        var dialect = _generator.Dialect;
        var visitor = new PredicateVisitor<TEntity>(_mapping, dialect);
        var (_, parameters) = visitor.Translate(_predicate, _ignoreCase);
        return parameters;
    }

    private string BuildPaginatedSql(string baseSql, int offset, int fetch)
    {
        var dialect = _generator.Dialect.Name;

        if (string.Equals(dialect, "Oracle", StringComparison.OrdinalIgnoreCase))
        {
            if (offset == 0 && fetch != int.MaxValue)
            {
                return $"{baseSql} FETCH FIRST {fetch} ROWS ONLY";
            }
            else if (offset > 0 && fetch == int.MaxValue)
            {
                return $"{baseSql} OFFSET {offset} ROWS";
            }
            else if (offset > 0 && fetch != int.MaxValue)
            {
                return $"{baseSql} OFFSET {offset} ROWS FETCH NEXT {fetch} ROWS ONLY";
            }
        }
        else
        {
            if (offset == 0 && fetch != int.MaxValue)
            {
                return $"{baseSql} OFFSET 0 ROWS FETCH NEXT {fetch} ROWS ONLY";
            }
            else if (offset > 0)
            {
                var fetchCount = fetch == int.MaxValue ? 999999999 : fetch;
                return $"{baseSql} OFFSET {offset} ROWS FETCH NEXT {fetchCount} ROWS ONLY";
            }
        }

        return baseSql;
    }
}
