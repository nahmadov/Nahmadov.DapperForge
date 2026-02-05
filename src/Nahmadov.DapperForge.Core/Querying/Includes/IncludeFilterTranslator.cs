using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;

namespace Nahmadov.DapperForge.Core.Querying.Includes;

/// <summary>
/// Translates Include filter expressions (from .Where() clauses) into SQL JOIN conditions.
/// </summary>
internal sealed class IncludeFilterTranslator : ExpressionVisitor
{
    private readonly ISqlDialect _dialect;
    private readonly Dictionary<PropertyInfo, PropertyMapping> _propertyLookup;
    private readonly string _alias;
    private readonly StringBuilder _sql = new();

    public IncludeFilterTranslator(
        ISqlDialect dialect,
        EntityMapping mapping,
        string alias)
    {
        _dialect = dialect;
        _alias = alias;
        _propertyLookup = mapping.PropertyMappings.ToDictionary(
            pm => pm.Property,
            pm => pm,
            PropertyInfoEqualityComparer.Instance);
    }

    /// <summary>
    /// Translates a filter lambda expression into a SQL condition string.
    /// </summary>
    public string Translate(LambdaExpression filter)
    {
        _sql.Clear();
        Visit(filter.Body);
        return _sql.ToString();
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        // Handle null comparisons
        if (IsNullComparison(node, out var memberExpr, out var isEqual) && memberExpr is not null)
        {
            AppendColumn(memberExpr);
            _sql.Append(isEqual ? " IS NULL" : " IS NOT NULL");
            return node;
        }

        _sql.Append('(');
        Visit(node.Left);
        _sql.Append(GetSqlOperator(node.NodeType));
        Visit(node.Right);
        _sql.Append(')');
        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        // Handle Nullable<T>.HasValue
        if (node.Member.Name == "HasValue" && node.Expression is MemberExpression innerHasValue)
        {
            AppendColumn(innerHasValue);
            _sql.Append(" IS NOT NULL");
            return node;
        }

        // Handle Nullable<T>.Value - just use the column
        if (node.Member.Name == "Value" && node.Expression is MemberExpression innerValue)
        {
            return Visit(innerValue);
        }

        // Handle boolean property access (e.g., m.IsActive where IsActive is bool)
        if (node.Member is PropertyInfo prop && IsEntityProperty(node))
        {
            var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            if (propType == typeof(bool))
            {
                AppendColumn(node);
                _sql.Append(" = ");
                _sql.Append(_dialect.FormatBoolean(true));
                return node;
            }

            AppendColumn(node);
            return node;
        }

        // Handle closure/constant values
        if (IsClosureBasedExpression(node))
        {
            var value = EvaluateExpression(node);
            AppendValue(value);
            return node;
        }

        return base.VisitMember(node);
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        AppendValue(node.Value);
        return node;
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

        // Handle Convert expressions (boxing)
        if (node.NodeType == ExpressionType.Convert)
        {
            return Visit(node.Operand);
        }

        return base.VisitUnary(node);
    }

    private void AppendColumn(MemberExpression node)
    {
        var prop = (PropertyInfo)node.Member;
        if (!_propertyLookup.TryGetValue(prop, out var mapping))
            throw new InvalidOperationException($"No mapping found for property '{prop.Name}'.");

        _sql.Append(_alias);
        _sql.Append('.');
        _sql.Append(_dialect.QuoteIdentifier(mapping.ColumnName));
    }

    private void AppendValue(object? value)
    {
        if (value is null)
        {
            _sql.Append("NULL");
            return;
        }

        if (value is bool b)
        {
            _sql.Append(_dialect.FormatBoolean(b));
            return;
        }

        if (value is string s)
        {
            _sql.Append('\'');
            _sql.Append(s.Replace("'", "''"));
            _sql.Append('\'');
            return;
        }

        if (value is int or long or short or byte or decimal or double or float)
        {
            _sql.Append(value);
            return;
        }

        // Fallback for other types
        _sql.Append('\'');
        _sql.Append(value.ToString()?.Replace("'", "''") ?? "");
        _sql.Append('\'');
    }

    private bool IsEntityProperty(MemberExpression node)
    {
        return node.Expression is ParameterExpression &&
               node.Member is PropertyInfo prop &&
               _propertyLookup.ContainsKey(prop);
    }

    private static bool IsClosureBasedExpression(Expression? expr)
    {
        while (expr is MemberExpression member)
        {
            expr = member.Expression;
        }
        return expr is ConstantExpression;
    }

    private static bool IsNullComparison(BinaryExpression node, out MemberExpression? member, out bool isEqual)
    {
        member = null;
        isEqual = node.NodeType == ExpressionType.Equal;

        if (node.NodeType != ExpressionType.Equal && node.NodeType != ExpressionType.NotEqual)
            return false;

        if (node.Right is ConstantExpression { Value: null } && node.Left is MemberExpression leftMember)
        {
            member = leftMember;
            return true;
        }

        if (node.Left is ConstantExpression { Value: null } && node.Right is MemberExpression rightMember)
        {
            member = rightMember;
            return true;
        }

        return false;
    }

    private static object? EvaluateExpression(Expression expr)
    {
        var lambda = Expression.Lambda(expr);
        var compiled = lambda.Compile();
        return compiled.DynamicInvoke();
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
        _ => throw new NotSupportedException($"Unsupported operator: {nodeType}")
    };
}

/// <summary>
/// Compares PropertyInfo instances by name and declaring type.
/// </summary>
file sealed class PropertyInfoEqualityComparer : IEqualityComparer<PropertyInfo>
{
    public static PropertyInfoEqualityComparer Instance { get; } = new();

    public bool Equals(PropertyInfo? x, PropertyInfo? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        return x.Name == y.Name && x.DeclaringType == y.DeclaringType;
    }

    public int GetHashCode(PropertyInfo obj)
    {
        return HashCode.Combine(obj.Name, obj.DeclaringType);
    }
}
