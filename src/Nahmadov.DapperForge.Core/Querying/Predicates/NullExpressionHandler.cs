using System.Linq.Expressions;

namespace Nahmadov.DapperForge.Core.Querying.Predicates;
/// <summary>
/// Handles null comparison expressions.
/// </summary>
internal sealed class NullExpressionHandler
{
    private readonly SqlExpressionBuilder _sqlBuilder;
    private readonly bool _treatEmptyStringAsNull;
    private readonly Func<Expression?, Expression?> _visitExpression;

    public NullExpressionHandler(
        SqlExpressionBuilder sqlBuilder,
        bool treatEmptyStringAsNull,
        Func<Expression?, Expression?> visitExpression)
    {
        _sqlBuilder = sqlBuilder;
        _treatEmptyStringAsNull = treatEmptyStringAsNull;
        _visitExpression = visitExpression;
    }

    public bool TryHandleNullComparison(BinaryExpression node)
    {
        if (node.NodeType is not (ExpressionType.Equal or ExpressionType.NotEqual))
            return false;

        if (IsNullLikeExpression(node.Right))
        {
            AppendNullComparison(node.Left, node.NodeType == ExpressionType.Equal);
            return true;
        }

        if (IsNullLikeExpression(node.Left))
        {
            AppendNullComparison(node.Right, node.NodeType == ExpressionType.Equal);
            return true;
        }

        return false;
    }

    public bool IsNullLikeExpression(Expression expr)
    {
        if (expr is ConstantExpression constant)
            return IsNullLikeValue(constant.Value);

        if (expr is MemberExpression { Expression: ConstantExpression closure } member)
        {
            var value = ExpressionEvaluator.GetValueFromClosure(closure.Value, member.Member);
            return IsNullLikeValue(value);
        }

        return false;
    }

    public bool IsNullLikeValue(object? value)
        => value is null || (_treatEmptyStringAsNull && value is string s && s.Length == 0);

    private void AppendNullComparison(Expression expr, bool isEqual)
    {
        _sqlBuilder.AppendSql('(');
        _visitExpression(expr);
        _sqlBuilder.AppendSql(isEqual ? " IS NULL)" : " IS NOT NULL)");
    }
}

