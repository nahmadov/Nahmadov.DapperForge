using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.Core.Modeling.Mapping;
using Nahmadov.DapperForge.Core.Querying.Includes;
using Nahmadov.DapperForge.Core.Querying.Predicates;
using Nahmadov.DapperForge.Core.Querying.Sql;

namespace Nahmadov.DapperForge.Core.Querying.Execution;
/// <summary>
/// Coordinates query execution strategies (simple, single-query includes, split-query includes).
/// </summary>
internal sealed class QueryExecutionCoordinator<TEntity> where TEntity : class
{
    private readonly DapperDbContext _context;
    private readonly SqlGenerator<TEntity> _generator;
    private readonly EntityMapping _mapping;
    private readonly QuerySqlBuilder<TEntity> _sqlBuilder;

    internal QueryExecutionCoordinator(
        DapperDbContext context,
        SqlGenerator<TEntity> generator,
        EntityMapping mapping,
        QuerySqlBuilder<TEntity> sqlBuilder)
    {
        _context = context;
        _generator = generator;
        _mapping = mapping;
        _sqlBuilder = sqlBuilder;
    }

    internal string BuildSql(QueryState<TEntity> state) => _sqlBuilder.BuildSql(state);

    internal async Task<IEnumerable<TEntity>> ExecuteSimpleQueryAsync(QueryState<TEntity> state)
    {
        var sql = _sqlBuilder.BuildSql(state);
        var parameters = _sqlBuilder.BuildParameters(state);
        return await _context.QueryAsync<TEntity>(sql, parameters).ConfigureAwait(false);
    }

    internal async Task<TEntity?> ExecuteFirstOrDefaultAsync(QueryState<TEntity> state)
    {
        var sql = _sqlBuilder.BuildSql(state);
        var parameters = _sqlBuilder.BuildParameters(state);
        return await _context.QueryFirstOrDefaultAsync<TEntity>(sql, parameters).ConfigureAwait(false);
    }

    internal async Task<long> ExecuteCountAsync(QueryState<TEntity> state)
    {
        var baseSql = $"SELECT COUNT(*) FROM {_generator.TableName} AS a";

        if (state.Predicate is null)
            return await _context.QueryFirstOrDefaultAsync<long>(baseSql, new Dictionary<string, object?>()).ConfigureAwait(false);

        var visitor = new PredicateVisitor<TEntity>(_mapping, _generator.Dialect);
        var (whereClause, parameters) = visitor.Translate(state.Predicate, state.IgnoreCase);

        return await _context.QueryFirstOrDefaultAsync<long>($"{baseSql} WHERE {whereClause}", parameters).ConfigureAwait(false);
    }

    internal async Task<List<TEntity>> ExecuteSingleQueryWithIncludesAsync(
        QueryState<TEntity> state,
        IncludeTree includeTree)
    {
        var planBuilder = new SingleQueryPlanBuilder(_generator.Dialect, _context.GetEntityMapping);
        var plan = planBuilder.Build(_mapping, includeTree);

        var (sql, parameters) = _sqlBuilder.ApplyWhereOrderPaging(plan.Sql, state);

        var executor = new SingleQueryIncludeExecutor(_context);
        return await executor.ExecuteAsync<TEntity>(
            sql: sql,
            parameters: parameters,
            splitOn: plan.SplitOn,
            rootMapping: _mapping,
            includeTree: includeTree).ConfigureAwait(false);
    }

    internal async Task<List<TEntity>> ExecuteSplitQueryWithIncludesAsync(
        QueryState<TEntity> state,
        IncludeTree includeTree,
        bool identityResolution)
    {
        var results = await ExecuteSimpleQueryAsync(state).ConfigureAwait(false);
        var list = results.ToList();

        if (list.Count == 0)
            return list;

        var loader = new SplitIncludeLoader(_context, _generator.Dialect, identityResolution);
        await loader.LoadAsync(_mapping, list, includeTree).ConfigureAwait(false);

        return list;
    }
}


