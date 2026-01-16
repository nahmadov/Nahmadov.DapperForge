using Nahmadov.DapperForge.Core.Querying.Execution;
using Nahmadov.DapperForge.Core.Querying.Predicates;
using Nahmadov.DapperForge.Core.Modeling.Mapping;

namespace Nahmadov.DapperForge.Core.Querying.Sql;
/// <summary>
/// Builds SQL and parameters for a queryable entity based on captured state.
/// </summary>
/// <typeparam name="TEntity">Entity type.</typeparam>
internal sealed class QuerySqlBuilder<TEntity>(SqlGenerator<TEntity> generator, EntityMapping mapping) where TEntity : class
{
    private readonly SqlGenerator<TEntity> _generator = generator;
    private readonly EntityMapping _mapping = mapping;

    public string BuildSql(QueryState<TEntity> state)
    {
        var sql = AddDistinctIfRequested(_generator.SelectAllSql, state.Distinct);

        sql = AppendWhere(sql, state);
        sql = AppendOrderBy(sql, state);
        sql = AppendPaging(sql, state);

        return sql;
    }

    public object BuildParameters(QueryState<TEntity> state)
    {
        if (state.Predicate is null)
            return new Dictionary<string, object?>();

        var visitor = new PredicateVisitor<TEntity>(_mapping, _generator.Dialect);
        var (_, parameters) = visitor.Translate(state.Predicate, state.IgnoreCase);
        return parameters;
    }

    public (string sql, object parameters) ApplyWhereOrderPaging(string baseSql, QueryState<TEntity> state)
    {
        var sql = AppendWhere(baseSql, state);
        sql = AppendOrderBy(sql, state);
        sql = AppendPaging(sql, state);

        return (sql, BuildParameters(state));
    }

    private static string AddDistinctIfRequested(string sql, bool distinct)
    {
        if (!distinct || !sql.StartsWith("SELECT ", StringComparison.OrdinalIgnoreCase))
        {
            return sql;
        }

        return string.Concat("SELECT DISTINCT ", sql.AsSpan(7));
    }

    private string AppendWhere(string sql, QueryState<TEntity> state)
    {
        if (state.Predicate is null)
            return sql;

        var visitor = new PredicateVisitor<TEntity>(_mapping, _generator.Dialect);
        var (whereClause, _) = visitor.Translate(state.Predicate, state.IgnoreCase);

        return $"{sql} WHERE {whereClause}";
    }

    private string AppendOrderBy(string sql, QueryState<TEntity> state)
    {
        if (state.OrderBy.Count > 0)
        {
            var visitor = new OrderingVisitor<TEntity>(_mapping, _generator.Dialect);
            var orderClauses = new List<string>();

            foreach (var (keySelector, isDescending) in state.OrderBy)
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
        else if (state.NeedsPaging && _mapping.KeyProperties.Count > 0)
        {
            var keyProp = _mapping.KeyProperties[0];
            var keyMapping = _mapping.PropertyMappings.First(pm => pm.Property == keyProp);
            var orderClause = $"a.{_generator.Dialect.QuoteIdentifier(keyMapping.ColumnName)}";

            return $"{sql} ORDER BY {orderClause}";
        }

        return sql;
    }

    private string AppendPaging(string sql, QueryState<TEntity> state)
    {
        if (!state.NeedsPaging)
            return sql;

        var isOracle = string.Equals(_generator.Dialect.Name, "Oracle", StringComparison.OrdinalIgnoreCase);

        if (state.Skip == 0 && state.Take != int.MaxValue)
        {
            return isOracle
                ? $"{sql} FETCH FIRST {state.Take} ROWS ONLY"
                : $"{sql} OFFSET 0 ROWS FETCH NEXT {state.Take} ROWS ONLY";
        }

        if (state.Skip > 0 && state.Take == int.MaxValue)
        {
            return $"{sql} OFFSET {state.Skip} ROWS";
        }

        if (state.Skip > 0 && state.Take != int.MaxValue)
        {
            return $"{sql} OFFSET {state.Skip} ROWS FETCH NEXT {state.Take} ROWS ONLY";
        }

        return sql;
    }
}


