using System.Linq.Expressions;

namespace Nahmadov.DapperForge.Core.Querying.Predicates;
/// <summary>
/// Handles boolean expression comparisons and projections.
/// </summary>
internal sealed class BooleanExpressionHandler
{
    private readonly SqlExpressionBuilder _sqlBuilder;

    public BooleanExpressionHandler(SqlExpressionBuilder sqlBuilder)
        => _sqlBuilder = sqlBuilder;

    public bool TryHandleBooleanProjection(Expression body)
    {
        if (body is MemberExpression member && _sqlBuilder.IsEntityBoolProperty(member))
        {
            AppendBooleanComparison(member, true);
            return true;
        }

        if (body is UnaryExpression { NodeType: ExpressionType.Not, Operand: MemberExpression neg }
            && _sqlBuilder.IsEntityBoolProperty(neg))
        {
            AppendBooleanComparison(neg, false);
            return true;
        }

        return false;
    }

    public bool TryHandleBooleanComparison(BinaryExpression node)
    {
        if (node.NodeType is not (ExpressionType.Equal or ExpressionType.NotEqual))
            return false;

        if (!IsBooleanComparison(node, out var memberExpr, out var value))
            return false;

        var column = _sqlBuilder.GetColumnNameForMember(memberExpr);
        _sqlBuilder.AppendSql($"({column} = {_sqlBuilder.Dialect.FormatBoolean(value)})");
        return true;
    }

    private void AppendBooleanComparison(MemberExpression member, bool value)
    {
        var column = _sqlBuilder.GetColumnNameForMember(member);
        _sqlBuilder.AppendSql($"{column} = {_sqlBuilder.Dialect.FormatBoolean(value)}");
    }

    private bool IsBooleanComparison(BinaryExpression node, out MemberExpression member, out bool value)
    {
        if (node.Left is MemberExpression left && _sqlBuilder.IsEntityBoolProperty(left)
            && ExpressionEvaluator.TryEvalToBool(node.Right, out value))
        {
            member = left;
            return true;
        }

        if (node.Right is MemberExpression right && _sqlBuilder.IsEntityBoolProperty(right)
            && ExpressionEvaluator.TryEvalToBool(node.Left, out value))
        {
            member = right;
            return true;
        }

        member = null!;
        value = false;
        return false;
    }
}
