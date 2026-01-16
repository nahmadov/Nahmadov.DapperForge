using System.Linq.Expressions;
using System.Reflection;

namespace Nahmadov.DapperForge.Core.Builders.Predicate;

/// <summary>
/// Handles boolean expression comparisons and projections.
/// </summary>
internal sealed class BooleanExpressionHandler<TEntity> where TEntity : class
{
    private readonly SqlExpressionBuilder _sqlBuilder;

    public BooleanExpressionHandler(SqlExpressionBuilder sqlBuilder)
    {
        _sqlBuilder = sqlBuilder;
    }

    public bool TryHandleBooleanProjection(Expression body)
    {
        if (body is MemberExpression member && IsEntityBooleanMember(member))
        {
            AppendBooleanComparison(member, true);
            return true;
        }

        if (body is UnaryExpression { NodeType: ExpressionType.Not, Operand: MemberExpression neg } &&
            IsEntityBooleanMember(neg))
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

    public bool IsEntityBooleanMember(MemberExpression node)
    {
        if (!EntityPropertyHelper.IsEntityProperty<TEntity>(node))
            return false;

        var propertyType = ((PropertyInfo)node.Member).PropertyType;
        var underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        return underlying == typeof(bool);
    }

    private void AppendBooleanComparison(MemberExpression member, bool value)
    {
        var column = _sqlBuilder.GetColumnNameForMember(member);
        _sqlBuilder.AppendSql($"{column} = {_sqlBuilder.Dialect.FormatBoolean(value)}");
    }

    private bool IsBooleanComparison(BinaryExpression node, out MemberExpression member, out bool value)
    {
        if (node.Left is MemberExpression left && IsEntityBooleanMember(left) &&
            ExpressionEvaluator.TryEvalToBool(node.Right, out value))
        {
            member = left;
            return true;
        }

        if (node.Right is MemberExpression right && IsEntityBooleanMember(right) &&
            ExpressionEvaluator.TryEvalToBool(node.Left, out value))
        {
            member = right;
            return true;
        }

        member = null!;
        value = false;
        return false;
    }
}
