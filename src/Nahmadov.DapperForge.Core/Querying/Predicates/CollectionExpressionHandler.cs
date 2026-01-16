using System.Collections;
using System.Linq.Expressions;

namespace Nahmadov.DapperForge.Core.Querying.Predicates;
/// <summary>
/// Handles collection Contains expressions for IN clauses.
/// </summary>
internal sealed class CollectionExpressionHandler<TEntity> where TEntity : class
{
    private readonly SqlExpressionBuilder _sqlBuilder;

    public CollectionExpressionHandler(SqlExpressionBuilder sqlBuilder)
    {
        _sqlBuilder = sqlBuilder;
    }

    public bool IsEnumerableContains(MethodCallExpression node, out MemberExpression member, out Expression valuesExpr)
    {
        member = null!;
        valuesExpr = null!;

        // Pattern: Enumerable.Contains(list, entity.Property)
        if (node.Method.Name == nameof(Enumerable.Contains) && node.Arguments.Count == 2)
        {
            if (node.Arguments[1] is MemberExpression me && EntityPropertyHelper.IsEntityProperty<TEntity>(me))
            {
                member = me;
                valuesExpr = node.Arguments[0];
                return true;
            }
        }

        // Pattern: list.Contains(entity.Property)
        if (node.Method.Name == nameof(List<int>.Contains) && node.Object is not null && node.Arguments.Count == 1)
        {
            if (node.Arguments[0] is MemberExpression me && EntityPropertyHelper.IsEntityProperty<TEntity>(me))
            {
                member = me;
                valuesExpr = node.Object;
                return true;
            }
        }

        // Generic pattern: collection.Contains(entity.Property)
        if (node.Method.Name == "Contains" && node.Object is not null && node.Arguments.Count == 1)
        {
            if (node.Arguments[0] is MemberExpression me && EntityPropertyHelper.IsEntityProperty<TEntity>(me))
            {
                member = me;
                valuesExpr = node.Object;
                return true;
            }
        }

        return false;
    }

    public void AppendInClause(MemberExpression memberExpr, Expression valuesExpr)
    {
        var column = _sqlBuilder.GetColumnNameForMember(memberExpr);
        var rawValues = ExpressionEvaluator.Evaluate(valuesExpr)
            ?? throw new InvalidOperationException("IN values cannot be null.");

        if (rawValues is string)
            throw new NotSupportedException("String is not supported for IN; use a collection instead.");

        if (rawValues is not IEnumerable enumerable)
            throw new NotSupportedException("IN operator requires an IEnumerable of values.");

        var list = enumerable.Cast<object?>().ToList();
        if (list.Count == 0)
        {
            _sqlBuilder.AppendSql("1=0");
            return;
        }

        var paramSql = _sqlBuilder.AddParameter(rawValues);
        _sqlBuilder.AppendSql($"{column} IN {paramSql}");
    }
}

