using System.Linq.Expressions;
using System.Reflection;

using Nahmadov.DapperForge.Core.Builders;
using Nahmadov.DapperForge.Core.Interfaces;
using Nahmadov.DapperForge.Core.Mapping;
using Nahmadov.DapperForge.Core.Query;

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

    private readonly IncludeTree _includeTree = new();
    private IncludeNode? _lastIncludeNode;

    private QuerySplittingBehavior _splittingBehavior = QuerySplittingBehavior.SplitQuery;
    private bool _identityResolution = true;

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

    public IDapperQueryable<TEntity> AsSplitQuery()
    {
        _splittingBehavior = QuerySplittingBehavior.SplitQuery;
        return this;
    }

    public IDapperQueryable<TEntity> AsSingleQuery()
    {
        _splittingBehavior = QuerySplittingBehavior.SingleQuery;
        return this;
    }

    public IDapperQueryable<TEntity> AsNoIdentityResolution()
    {
        _identityResolution = false;
        return this;
    }

    public IIncludableQueryable<TEntity, TRelated?> Include<TRelated>(Expression<Func<TEntity, TRelated?>> navigationSelector)
        where TRelated : class
    {
        ArgumentNullException.ThrowIfNull(navigationSelector);

        var navProp = ExtractProperty(navigationSelector.Body);

        var node = new IncludeNode
        {
            Navigation = navProp,
            RelatedType = typeof(TRelated),
            IsCollection = false
        };

        _includeTree.Roots.Add(node);
        _lastIncludeNode = node;

        return new IncludableQueryable<TEntity, TRelated?>(this);
    }

    public IIncludableQueryable<TEntity, IEnumerable<TRelated>> Include<TRelated>(Expression<Func<TEntity, IEnumerable<TRelated>>> navigationSelector)
        where TRelated : class
    {
        ArgumentNullException.ThrowIfNull(navigationSelector);

        var navProp = ExtractProperty(navigationSelector.Body);

        var node = new IncludeNode
        {
            Navigation = navProp,
            RelatedType = typeof(TRelated),
            IsCollection = true
        };

        _includeTree.Roots.Add(node);
        _lastIncludeNode = node;

        return new IncludableQueryable<TEntity, IEnumerable<TRelated>>(this);
    }

    public IIncludableQueryable<TEntity, TNext?> ThenInclude<TPrevious, TNext>(Expression<Func<TPrevious, TNext?>> navigationSelector)
        where TPrevious : class
        where TNext : class
    {
        ArgumentNullException.ThrowIfNull(navigationSelector);

        if (_lastIncludeNode is null)
            throw new InvalidOperationException("ThenInclude must be called after Include.");

        var navProp = ExtractProperty(navigationSelector.Body);

        var node = new IncludeNode
        {
            Navigation = navProp,
            RelatedType = typeof(TNext),
            IsCollection = false
        };

        _lastIncludeNode.Children.Add(node);
        _lastIncludeNode = node;

        return new IncludableQueryable<TEntity, TNext?>(this);
    }

    public IIncludableQueryable<TEntity, IEnumerable<TNext>> ThenInclude<TPrevious, TNext>(Expression<Func<TPrevious, IEnumerable<TNext>>> navigationSelector)
        where TPrevious : class
        where TNext : class
    {
        ArgumentNullException.ThrowIfNull(navigationSelector);

        if (_lastIncludeNode is null)
            throw new InvalidOperationException("ThenInclude must be called after Include.");

        var navProp = ExtractProperty(navigationSelector.Body);

        var node = new IncludeNode
        {
            Navigation = navProp,
            RelatedType = typeof(TNext),
            IsCollection = true
        };

        _lastIncludeNode.Children.Add(node);
        _lastIncludeNode = node;

        return new IncludableQueryable<TEntity, IEnumerable<TNext>>(this);
    }

    public async Task<IEnumerable<TEntity>> ToListAsync()
    {
        var sql = BuildSql();
        var parameters = GetParameters();

        var results = await _context.QueryAsync<TEntity>(sql, parameters);
        var list = results.ToList();

        if (list.Count == 0 || _includeTree.Roots.Count == 0)
            return list;

        if (_splittingBehavior == QuerySplittingBehavior.SingleQuery)
            throw new NotSupportedException("AsSingleQuery is not implemented yet. It requires join SQL + multi-mapping + graph fixup.");

        var loader = new SplitIncludeLoader(_context, _generator.Dialect, _identityResolution);
        await loader.LoadAsync(_mapping, list, _includeTree);

        return list;
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

    private static PropertyInfo ExtractProperty(Expression body)
    {
        if (body is MemberExpression me && me.Member is PropertyInfo pi)
            return pi;

        // Handle boxing/unary
        if (body is UnaryExpression ue && ue.Operand is MemberExpression me2 && me2.Member is PropertyInfo pi2)
            return pi2;

        throw new NotSupportedException($"Expression '{body}' is not a property access.");
    }
}