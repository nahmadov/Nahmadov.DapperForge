using System.Linq.Expressions;
using System.Reflection;

using Nahmadov.DapperForge.Core.Builders;
using Nahmadov.DapperForge.Core.Interfaces;
using Nahmadov.DapperForge.Core.Mapping;
using Nahmadov.DapperForge.Core.Query;

namespace Nahmadov.DapperForge.Core.Context;

/// <summary>
/// Fluent query builder for DapperForge queries.
/// Supports Where, OrderBy, Skip, Take, Include, and ThenInclude operations.
/// </summary>
internal sealed class DapperQueryable<TEntity> : IDapperQueryable<TEntity> where TEntity : class
{
    private readonly DapperDbContext _context;
    private readonly SqlGenerator<TEntity> _generator;
    private readonly EntityMapping _mapping;

    private Expression<Func<TEntity, bool>>? _predicate;
    private readonly List<(Expression<Func<TEntity, object?>> keySelector, bool isDescending)> _orderByList = [];
    private int _skip;
    private int _take = int.MaxValue;
    private bool _ignoreCase;
    private bool _distinct;

    private readonly IncludeTree _includeTree = new();
    private IncludeNode? _lastIncludeNode;
    private QuerySplittingBehavior _splittingBehavior = QuerySplittingBehavior.SingleQuery;
    private bool _identityResolution = true;

    internal DapperQueryable(DapperDbContext context, SqlGenerator<TEntity> generator, EntityMapping mapping)
    {
        _context = context;
        _generator = generator;
        _mapping = mapping;
    }

    #region Query Configuration

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
        _orderByList.Clear();
        _orderByList.Add((keySelector, false));
        return this;
    }

    public IDapperQueryable<TEntity> OrderByDescending(Expression<Func<TEntity, object?>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        _orderByList.Clear();
        _orderByList.Add((keySelector, true));
        return this;
    }

    public IDapperQueryable<TEntity> ThenBy(Expression<Func<TEntity, object?>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        if (_orderByList.Count == 0)
            throw new InvalidOperationException("ThenBy must be called after OrderBy or OrderByDescending.");
        _orderByList.Add((keySelector, false));
        return this;
    }

    public IDapperQueryable<TEntity> ThenByDescending(Expression<Func<TEntity, object?>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        if (_orderByList.Count == 0)
            throw new InvalidOperationException("ThenByDescending must be called after OrderBy or OrderByDescending.");
        _orderByList.Add((keySelector, true));
        return this;
    }

    public IDapperQueryable<TEntity> Distinct()
    {
        _distinct = true;
        return this;
    }

    public IDapperQueryable<TEntity> Skip(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Skip count cannot be negative.");
        _skip = count;
        return this;
    }

    public IDapperQueryable<TEntity> Take(int count)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "Take count must be 1 or greater.");
        _take = count;
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

    #endregion

    #region Include / ThenInclude

    public IIncludableQueryable<TEntity, TProperty> Include<TProperty>(
        Expression<Func<TEntity, TProperty>> navigationSelector)
    {
        ArgumentNullException.ThrowIfNull(navigationSelector);

        var (navigation, relatedType, isCollection) = ExtractNavigationInfo(navigationSelector.Body);

        _lastIncludeNode = _includeTree.AddRoot(navigation, relatedType, isCollection);

        return new IncludableQueryable<TEntity, TProperty>(this);
    }

    public IIncludableQueryable<TEntity, TNextProperty> ThenInclude<TPrevious, TNextProperty>(
        Expression<Func<TPrevious, TNextProperty>> navigationSelector)
        where TPrevious : class
    {
        ArgumentNullException.ThrowIfNull(navigationSelector);

        if (_lastIncludeNode is null)
            throw new InvalidOperationException("ThenInclude must be called after Include.");

        var (navigation, relatedType, isCollection) = ExtractNavigationInfo(navigationSelector.Body);

        _lastIncludeNode = _lastIncludeNode.AddChild(navigation, relatedType, isCollection);

        return new IncludableQueryable<TEntity, TNextProperty>(this);
    }

    private static (PropertyInfo navigation, Type relatedType, bool isCollection) ExtractNavigationInfo(Expression body)
    {
        var navigation = ExtractProperty(body);
        var isCollection = CollectionHelper.IsCollectionType(navigation.PropertyType);

        var relatedType = isCollection
            ? CollectionHelper.GetElementType(navigation.PropertyType, navigation.Name)
            : navigation.PropertyType;

        return (navigation, relatedType, isCollection);
    }

    #endregion

    #region Execution

    public async Task<IEnumerable<TEntity>> ToListAsync()
    {
        if (!_includeTree.HasIncludes)
        {
            return await ExecuteSimpleQueryAsync();
        }

        return _splittingBehavior == QuerySplittingBehavior.SingleQuery
            ? await ExecuteSingleQueryWithIncludesAsync()
            : await ExecuteSplitQueryWithIncludesAsync();
    }

    public async Task<TEntity> FirstAsync()
    {
        var result = await FirstOrDefaultAsync();
        if (result is null)
            throw new InvalidOperationException("Sequence contains no elements.");
        return result;
    }

    public async Task<TEntity?> FirstOrDefaultAsync()
    {
        var sql = BuildSql();
        var parameters = BuildParameters();
        return await _context.QueryFirstOrDefaultAsync<TEntity>(sql, parameters);
    }

    public async Task<TEntity> SingleAsync()
    {
        var result = await SingleOrDefaultAsync();
        if (result is null)
            throw new InvalidOperationException("Sequence contains no elements.");
        return result;
    }

    public async Task<TEntity?> SingleOrDefaultAsync()
    {
        var results = await ExecuteSimpleQueryAsync();
        var list = results.ToList();

        if (list.Count > 1)
            throw new InvalidOperationException(
                $"Sequence contains more than one element.");

        return list.FirstOrDefault();
    }

    public async Task<TEntity> LastAsync()
    {
        var result = await LastOrDefaultAsync();
        if (result is null)
            throw new InvalidOperationException("Sequence contains no elements.");
        return result;
    }

    public async Task<TEntity?> LastOrDefaultAsync()
    {
        var results = await ExecuteSimpleQueryAsync();
        var list = results.ToList();
        return list.LastOrDefault();
    }

    public async Task<bool> AnyAsync()
    {
        var count = await CountAsync();
        return count > 0;
    }

    public async Task<long> CountAsync()
    {
        var baseSql = $"SELECT COUNT(*) FROM {_generator.TableName} AS a";

        if (_predicate is null)
            return await _context.QueryFirstOrDefaultAsync<long>(baseSql, new Dictionary<string, object?>());

        var visitor = new PredicateVisitor<TEntity>(_mapping, _generator.Dialect);
        var (whereClause, parameters) = visitor.Translate(_predicate, _ignoreCase);

        return await _context.QueryFirstOrDefaultAsync<long>($"{baseSql} WHERE {whereClause}", parameters);
    }

    private async Task<IEnumerable<TEntity>> ExecuteSimpleQueryAsync()
    {
        var sql = BuildSql();
        var parameters = BuildParameters();
        return await _context.QueryAsync<TEntity>(sql, parameters);
    }

    private async Task<List<TEntity>> ExecuteSingleQueryWithIncludesAsync()
    {
        var planBuilder = new SingleQueryPlanBuilder(_generator.Dialect, _context.GetEntityMapping);
        var plan = planBuilder.Build(_mapping, _includeTree);

        var (sql, parameters) = ApplyWhereOrderPaging(plan.Sql);

        var executor = new SingleQueryIncludeExecutor(_context);
        return await executor.ExecuteAsync<TEntity>(
            sql: sql,
            parameters: parameters,
            splitOn: plan.SplitOn,
            rootMapping: _mapping,
            includeTree: _includeTree);
    }

    private async Task<List<TEntity>> ExecuteSplitQueryWithIncludesAsync()
    {
        var results = await ExecuteSimpleQueryAsync();
        var list = results.ToList();

        if (list.Count == 0)
            return list;

        var loader = new SplitIncludeLoader(_context, _generator.Dialect, _identityResolution);
        await loader.LoadAsync(_mapping, list, _includeTree);

        return list;
    }

    #endregion

    #region SQL Building

    private string BuildSql()
    {
        var sql = _generator.SelectAllSql;

        // Add DISTINCT keyword if requested
        if (_distinct && sql.StartsWith("SELECT ", StringComparison.OrdinalIgnoreCase))
        {
            sql = string.Concat("SELECT DISTINCT ", sql.AsSpan(7));
        }

        sql = AppendWhere(sql);
        sql = AppendOrderBy(sql);
        sql = AppendPaging(sql);

        return sql;
    }

    private object BuildParameters()
    {
        if (_predicate is null)
            return new Dictionary<string, object?>();

        var visitor = new PredicateVisitor<TEntity>(_mapping, _generator.Dialect);
        var (_, parameters) = visitor.Translate(_predicate, _ignoreCase);
        return parameters;
    }

    private (string sql, object parameters) ApplyWhereOrderPaging(string baseSql)
    {
        var sql = AppendWhere(baseSql);
        sql = AppendOrderBy(sql);
        sql = AppendPaging(sql);

        return (sql, BuildParameters());
    }

    private string AppendWhere(string sql)
    {
        if (_predicate is null)
            return sql;

        var visitor = new PredicateVisitor<TEntity>(_mapping, _generator.Dialect);
        var (whereClause, _) = visitor.Translate(_predicate, _ignoreCase);

        return $"{sql} WHERE {whereClause}";
    }

    private string AppendOrderBy(string sql)
    {
        if (_orderByList.Count > 0)
        {
            var visitor = new OrderingVisitor<TEntity>(_mapping, _generator.Dialect);
            var orderClauses = new List<string>();

            foreach (var (keySelector, isDescending) in _orderByList)
            {
                var orderClause = visitor.Translate(keySelector, isDescending);
                if (!string.IsNullOrEmpty(orderClause))
                {
                    orderClauses.Add(orderClause);
                }
            }

            if (orderClauses.Count > 0)
            {
                return $"{sql} ORDER BY {string.Join(", ", orderClauses)}";
            }
        }
        else if (NeedsPaging && _mapping.KeyProperties.Count > 0)
        {
            var keyProp = _mapping.KeyProperties[0];
            var keyMapping = _mapping.PropertyMappings.First(pm => pm.Property == keyProp);
            var orderClause = $"a.{_generator.Dialect.QuoteIdentifier(keyMapping.ColumnName)}";

            return $"{sql} ORDER BY {orderClause}";
        }

        return sql;
    }

    private string AppendPaging(string sql)
    {
        if (!NeedsPaging)
            return sql;

        var isOracle = string.Equals(_generator.Dialect.Name, "Oracle", StringComparison.OrdinalIgnoreCase);

        if (_skip == 0 && _take != int.MaxValue)
        {
            return isOracle
                ? $"{sql} FETCH FIRST {_take} ROWS ONLY"
                : $"{sql} OFFSET 0 ROWS FETCH NEXT {_take} ROWS ONLY";
        }

        if (_skip > 0 && _take == int.MaxValue)
        {
            return $"{sql} OFFSET {_skip} ROWS";
        }

        if (_skip > 0 && _take != int.MaxValue)
        {
            var fetch = isOracle ? _take : _take;
            return $"{sql} OFFSET {_skip} ROWS FETCH NEXT {fetch} ROWS ONLY";
        }

        return sql;
    }

    private bool NeedsPaging => _skip > 0 || _take < int.MaxValue;

    #endregion

    #region Helpers

    private static PropertyInfo ExtractProperty(Expression body)
    {
        return body switch
        {
            MemberExpression { Member: PropertyInfo pi } => pi,
            UnaryExpression { Operand: MemberExpression { Member: PropertyInfo pi2 } } => pi2,
            _ => throw new NotSupportedException($"Expression '{body}' is not a property access.")
        };
    }

    #endregion
}
