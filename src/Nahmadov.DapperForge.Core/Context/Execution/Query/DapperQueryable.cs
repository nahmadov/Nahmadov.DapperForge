using System.Linq.Expressions;
using System.Reflection;

using Nahmadov.DapperForge.Core.Builders;
using Nahmadov.DapperForge.Core.Interfaces;
using Nahmadov.DapperForge.Core.Mapping;
using Nahmadov.DapperForge.Core.Query;

namespace Nahmadov.DapperForge.Core.Context.Execution.Query;

/// <summary>
/// Fluent query builder for DapperForge queries.
/// Supports Where, OrderBy, Skip, Take, Include, and ThenInclude operations.
/// </summary>
internal sealed class DapperQueryable<TEntity> : IDapperQueryable<TEntity> where TEntity : class
{
    private readonly QueryState<TEntity> _state = new();
    private readonly IncludeTree _includeTree = new();
    private readonly QueryExecutionCoordinator<TEntity> _executor;

    private IncludeNode? _lastIncludeNode;
    private QuerySplittingBehavior _splittingBehavior = QuerySplittingBehavior.SingleQuery;
    private bool _identityResolution = true;

    internal DapperQueryable(DapperDbContext context, SqlGenerator<TEntity> generator, EntityMapping mapping)
    {
        var sqlBuilder = new QuerySqlBuilder<TEntity>(generator, mapping);
        _executor = new QueryExecutionCoordinator<TEntity>(context, generator, mapping, sqlBuilder);
    }

    #region Query Configuration

    public IDapperQueryable<TEntity> Where(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _state.Predicate = predicate;
        _state.IgnoreCase = ignoreCase;
        return this;
    }

    public IDapperQueryable<TEntity> OrderBy(Expression<Func<TEntity, object?>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        _state.OrderBy.Clear();
        _state.OrderBy.Add((keySelector, false));
        return this;
    }

    public IDapperQueryable<TEntity> OrderByDescending(Expression<Func<TEntity, object?>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        _state.OrderBy.Clear();
        _state.OrderBy.Add((keySelector, true));
        return this;
    }

    public IDapperQueryable<TEntity> ThenBy(Expression<Func<TEntity, object?>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        if (_state.OrderBy.Count == 0)
            throw new InvalidOperationException("ThenBy must be called after OrderBy or OrderByDescending.");
        _state.OrderBy.Add((keySelector, false));
        return this;
    }

    public IDapperQueryable<TEntity> ThenByDescending(Expression<Func<TEntity, object?>> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);
        if (_state.OrderBy.Count == 0)
            throw new InvalidOperationException("ThenByDescending must be called after OrderBy or OrderByDescending.");
        _state.OrderBy.Add((keySelector, true));
        return this;
    }

    public IDapperQueryable<TEntity> Distinct()
    {
        _state.Distinct = true;
        return this;
    }

    public IDapperQueryable<TEntity> Skip(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Skip count cannot be negative.");
        _state.Skip = count;
        return this;
    }

    public IDapperQueryable<TEntity> Take(int count)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "Take count must be 1 or greater.");
        _state.Take = count;
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
            return await _executor.ExecuteSimpleQueryAsync(_state).ConfigureAwait(false);

        return _splittingBehavior == QuerySplittingBehavior.SingleQuery
            ? await _executor.ExecuteSingleQueryWithIncludesAsync(_state, _includeTree).ConfigureAwait(false)
            : await _executor.ExecuteSplitQueryWithIncludesAsync(_state, _includeTree, _identityResolution).ConfigureAwait(false);
    }

    public async Task<TEntity> FirstAsync()
    {
        var result = await FirstOrDefaultAsync().ConfigureAwait(false);
        if (result is null)
            throw new InvalidOperationException("Sequence contains no elements.");
        return result;
    }

    public async Task<TEntity?> FirstOrDefaultAsync()
    {
        return await _executor.ExecuteFirstOrDefaultAsync(_state).ConfigureAwait(false);
    }

    public async Task<TEntity> SingleAsync()
    {
        var result = await SingleOrDefaultAsync().ConfigureAwait(false);
        if (result is null)
            throw new InvalidOperationException("Sequence contains no elements.");
        return result;
    }

    public async Task<TEntity?> SingleOrDefaultAsync()
    {
        var results = await _executor.ExecuteSimpleQueryAsync(_state).ConfigureAwait(false);
        var list = results.ToList();

        if (list.Count > 1)
            throw new InvalidOperationException("Sequence contains more than one element.");

        return list.FirstOrDefault();
    }

    public async Task<TEntity> LastAsync()
    {
        var result = await LastOrDefaultAsync().ConfigureAwait(false);
        if (result is null)
            throw new InvalidOperationException("Sequence contains no elements.");
        return result;
    }

    public async Task<TEntity?> LastOrDefaultAsync()
    {
        var results = await _executor.ExecuteSimpleQueryAsync(_state).ConfigureAwait(false);
        var list = results.ToList();
        return list.LastOrDefault();
    }

    public async Task<bool> AnyAsync()
    {
        var count = await CountAsync().ConfigureAwait(false);
        return count > 0;
    }

    public async Task<long> CountAsync()
    {
        return await _executor.ExecuteCountAsync(_state).ConfigureAwait(false);
    }

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

    // Used by tests via reflection
    internal string BuildSql() => _executor.BuildSql(_state);

    #endregion
}
