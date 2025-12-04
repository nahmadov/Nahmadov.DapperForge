using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using DapperToolkit.Core.Interfaces;
using DapperToolkit.Core.Mapping;

namespace DapperToolkit.Core.Builders;

public sealed class PredicateVisitor<TEntity> : ExpressionVisitor
    where TEntity : class
{
    private readonly EntityMapping _mapping;
    private readonly ISqlDialect _dialect;
    private readonly Dictionary<PropertyInfo, PropertyMapping> _propertyLookup;

    private readonly StringBuilder _sql = new();
    private readonly Dictionary<string, object?> _parameters = new();
    private int _paramIndex;

    public PredicateVisitor(EntityMapping mapping, ISqlDialect dialect)
    {
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        _propertyLookup = _mapping.PropertyMappings.ToDictionary(pm => pm.Property, pm => pm);
    }

    public (string Sql, object Parameters) Translate(Expression<Func<TEntity, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        _sql.Clear();
        _parameters.Clear();
        _paramIndex = 0;

        if (!TryHandleBooleanProjection(predicate.Body))
        {
            Visit(predicate.Body);
        }

        return (_sql.ToString(), new Dictionary<string, object?>(_parameters));
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (TryHandleBooleanComparison(node))
            return node;

        if (TryHandleNullComparison(node))
            return node;

        _sql.Append('(');
        Visit(node.Left);
        _sql.Append(GetSqlOperator(node.NodeType));
        Visit(node.Right);
        _sql.Append(')');
        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (PredicateVisitor<TEntity>.IsEntityProperty(node))
        {
            AppendColumn((PropertyInfo)node.Member);
            return node;
        }

        if (node.Expression is ConstantExpression closure)
        {
            var value = GetValueFromClosure(closure.Value, node.Member);

            if (value is bool b)
            {
                AppendBooleanLiteral(b);
                return node;
            }

            if (value is null)
            {
                _sql.Append("NULL");
                return node;
            }

            AppendParameter(value);
            return node;
        }

        return base.VisitMember(node);
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value is bool b)
        {
            AppendBooleanLiteral(b);
            return node;
        }

        if (node.Value is null)
        {
            _sql.Append("NULL");
            return node;
        }

        AppendParameter(node.Value);
        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.Name == nameof(string.Contains) &&
            node.Object is MemberExpression memberExpr &&
            memberExpr.Expression is ParameterExpression)
        {
            AppendLikeContains(memberExpr, node.Arguments[0]);
            return node;
        }

        throw new NotSupportedException($"Method call '{node.Method.Name}' is not supported.");
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        if (node.NodeType == ExpressionType.Not)
        {
            _sql.Append("NOT (");
            Visit(node.Operand);
            _sql.Append(')');
            return node;
        }

        return base.VisitUnary(node);
    }

    private bool TryHandleBooleanProjection(Expression body)
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

    private bool TryHandleBooleanComparison(BinaryExpression node)
    {
        if (node.NodeType is not (ExpressionType.Equal or ExpressionType.NotEqual))
            return false;

        if (!IsBooleanComparison(node, out var memberExpr, out var value))
            return false;

        var column = GetColumnNameForMember(memberExpr);
        _sql.Append($"({column} = {_dialect.FormatBoolean(value)})");
        return true;
    }

    private bool TryHandleNullComparison(BinaryExpression node)
    {
        if (node.NodeType is not (ExpressionType.Equal or ExpressionType.NotEqual))
            return false;

        if (IsNullConstant(node.Right))
        {
            AppendNullComparison(node.Left, node.NodeType == ExpressionType.Equal);
            return true;
        }

        if (IsNullConstant(node.Left))
        {
            AppendNullComparison(node.Right, node.NodeType == ExpressionType.Equal);
            return true;
        }

        return false;
    }

    private void AppendNullComparison(Expression expr, bool isEqual)
    {
        _sql.Append('(');
        Visit(expr);
        _sql.Append(isEqual ? " IS NULL)" : " IS NOT NULL)");
    }

    private void AppendBooleanComparison(MemberExpression member, bool value)
    {
        var column = GetColumnNameForMember(member);
        _sql.Append($"{column} = {_dialect.FormatBoolean(value)}");
    }

    private void AppendColumn(PropertyInfo property)
    {
        if (!_propertyLookup.TryGetValue(property, out var map))
            throw new InvalidOperationException($"No mapping found for property '{property.Name}'.");

        _sql.Append(_dialect.QuoteIdentifier(map.ColumnName));
    }

    private static string GetSqlOperator(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.Equal => " = ",
        ExpressionType.NotEqual => " <> ",
        ExpressionType.GreaterThan => " > ",
        ExpressionType.GreaterThanOrEqual => " >= ",
        ExpressionType.LessThan => " < ",
        ExpressionType.LessThanOrEqual => " <= ",
        ExpressionType.AndAlso => " AND ",
        ExpressionType.OrElse => " OR ",
        _ => throw new NotSupportedException($"Unsupported node: {nodeType}")
    };

    private void AppendBooleanLiteral(bool value)
    {
        _sql.Append(value ? "1=1" : "1=0");
    }

    private bool IsBooleanComparison(BinaryExpression node, out MemberExpression member, out bool value)
    {
        if (node.Left is MemberExpression left && IsEntityBooleanMember(left) &&
            PredicateVisitor<TEntity>.TryEvalToBool(node.Right, out value))
        {
            member = left;
            return true;
        }

        if (node.Right is MemberExpression right && IsEntityBooleanMember(right) &&
            PredicateVisitor<TEntity>.TryEvalToBool(node.Left, out value))
        {
            member = right;
            return true;
        }

        member = null!;
        value = false;
        return false;
    }

    private static bool TryEvalToBool(Expression expr, out bool value)
    {
        var v = EvaluateExpression(expr);
        if (v is bool b)
        {
            value = b;
            return true;
        }

        value = false;
        return false;
    }

    private bool IsEntityBooleanMember(MemberExpression node)
    {
        if (!PredicateVisitor<TEntity>.IsEntityProperty(node))
            return false;

        var propertyType = ((PropertyInfo)node.Member).PropertyType;
        var underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        return underlying == typeof(bool);
    }

    private static bool IsEntityProperty(MemberExpression node)
    {
        return typeof(TEntity).IsAssignableFrom(node.Expression?.Type ?? typeof(object)) &&
               node.Member is PropertyInfo;
    }

    private string GetColumnNameForMember(MemberExpression node)
    {
        var prop = (PropertyInfo)node.Member;
        if (_propertyLookup.TryGetValue(prop, out var map))
            return _dialect.QuoteIdentifier(map.ColumnName);

        throw new InvalidOperationException($"No mapping found for property '{prop.Name}'.");
    }

    private void AppendLikeContains(MemberExpression memberExpr, Expression argument)
    {
        Visit(memberExpr);
        _sql.Append(" LIKE ");

        var raw = EvaluateExpression(argument);
        var escaped = EscapeLikeValue(raw?.ToString() ?? string.Empty);
        AppendParameter($"%{escaped}%");
        _sql.Append(" ESCAPE '\\\\'");
    }

    private static string EscapeLikeValue(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }

    private static bool IsNullConstant(Expression expr)
    {
        if (expr is ConstantExpression constant)
            return constant.Value is null;

        if (expr is MemberExpression member && member.Expression is ConstantExpression closure)
        {
            var value = GetValueFromClosure(closure.Value, member.Member);
            return value is null;
        }

        return EvaluateExpression(expr) is null;
    }

    private void AppendParameter(object? value)
    {
        var paramKey = $"p{_paramIndex++}";
        _parameters[paramKey] = value ?? DBNull.Value;
        _sql.Append(_dialect.FormatParameter(paramKey));
    }

    private static object? GetValueFromClosure(object? closureObject, MemberInfo member)
    {
        return closureObject is null
            ? null
            : member switch
            {
                FieldInfo fi => fi.GetValue(closureObject),
                PropertyInfo pi => pi.GetValue(closureObject),
                _ => throw new NotSupportedException($"Unsupported closure member type: {member.MemberType}")
            };
    }

    private static object? EvaluateExpression(Expression expr)
    {
        if (expr is ConstantExpression constant)
            return constant.Value;

        var lambda = Expression.Lambda(expr);
        var compiled = lambda.Compile();
        return compiled.DynamicInvoke();
    }
}
