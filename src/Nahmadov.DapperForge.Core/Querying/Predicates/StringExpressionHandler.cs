using System.Linq.Expressions;

namespace Nahmadov.DapperForge.Core.Querying.Predicates;
/// <summary>
/// Handles string expression operations including equality and LIKE patterns.
/// </summary>
internal sealed class StringExpressionHandler<TEntity> where TEntity : class
{
    private readonly SqlExpressionBuilder _sqlBuilder;
    private readonly NullExpressionHandler _nullHandler;
    private readonly Func<bool> _getIgnoreCase;

    public StringExpressionHandler(
        SqlExpressionBuilder sqlBuilder,
        NullExpressionHandler nullHandler,
        Func<bool> getIgnoreCase)
    {
        _sqlBuilder = sqlBuilder;
        _nullHandler = nullHandler;
        _getIgnoreCase = getIgnoreCase;
    }

    public bool TryHandleStringEquality(BinaryExpression node)
    {
        if (node.NodeType is not (ExpressionType.Equal or ExpressionType.NotEqual))
            return false;

        MemberExpression? member = null;
        Expression? other = null;

        if (node.Left is MemberExpression ml && EntityPropertyHelper.IsStringProperty<TEntity>(ml))
        {
            member = ml;
            other = node.Right;
        }
        else if (node.Right is MemberExpression mr && EntityPropertyHelper.IsStringProperty<TEntity>(mr))
        {
            member = mr;
            other = node.Left;
        }

        if (member is null || other is null)
            return false;

        var column = _sqlBuilder.GetColumnNameForMember(member);
        var ignoreCase = _getIgnoreCase();
        var left = ignoreCase ? $"LOWER({column})" : column;

        var value = ExpressionEvaluator.Evaluate(other);
        if (_nullHandler.IsNullLikeValue(value))
        {
            _sqlBuilder.AppendSql($"({column} {(node.NodeType == ExpressionType.Equal ? "IS NULL" : "IS NOT NULL")})");
            return true;
        }

        var normalized = NormalizeForCase(value, ignoreCase);
        var paramSql = _sqlBuilder.AddParameter(normalized);
        var op = node.NodeType == ExpressionType.Equal ? "=" : "<>";
        _sqlBuilder.AppendSql($"({left} {op} {paramSql})");
        return true;
    }

    public void AppendLikeContains(MemberExpression memberExpr, Expression argument)
    {
        var column = _sqlBuilder.GetColumnNameForMember(memberExpr);
        var raw = ExpressionEvaluator.Evaluate(argument);
        var escaped = EscapeLikeValue(raw?.ToString() ?? string.Empty);
        var ignoreCase = _getIgnoreCase();
        var normalized = NormalizeForCase($"%{escaped}%", ignoreCase);
        var paramSql = _sqlBuilder.AddParameter(normalized);

        var left = ignoreCase ? $"LOWER({column})" : column;
        _sqlBuilder.AppendSql($"{left} LIKE {paramSql} ESCAPE '\\'");
    }

    public void AppendLikeStartsWith(MemberExpression memberExpr, Expression argument)
    {
        var column = _sqlBuilder.GetColumnNameForMember(memberExpr);
        var raw = ExpressionEvaluator.Evaluate(argument);
        var escaped = EscapeLikeValue(raw?.ToString() ?? string.Empty);
        var ignoreCase = _getIgnoreCase();
        var normalized = NormalizeForCase($"{escaped}%", ignoreCase);
        var paramSql = _sqlBuilder.AddParameter(normalized);

        var left = ignoreCase ? $"LOWER({column})" : column;
        _sqlBuilder.AppendSql($"{left} LIKE {paramSql} ESCAPE '\\'");
    }

    public void AppendLikeEndsWith(MemberExpression memberExpr, Expression argument)
    {
        var column = _sqlBuilder.GetColumnNameForMember(memberExpr);
        var raw = ExpressionEvaluator.Evaluate(argument);
        var escaped = EscapeLikeValue(raw?.ToString() ?? string.Empty);
        var ignoreCase = _getIgnoreCase();
        var normalized = NormalizeForCase($"%{escaped}", ignoreCase);
        var paramSql = _sqlBuilder.AddParameter(normalized);

        var left = ignoreCase ? $"LOWER({column})" : column;
        _sqlBuilder.AppendSql($"{left} LIKE {paramSql} ESCAPE '\\'");
    }

    private static string EscapeLikeValue(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }

    private static object? NormalizeForCase(object? value, bool ignoreCase)
    {
        if (!ignoreCase)
            return value;

        if (value is string str)
            return str.ToLowerInvariant();

        return value;
    }
}

