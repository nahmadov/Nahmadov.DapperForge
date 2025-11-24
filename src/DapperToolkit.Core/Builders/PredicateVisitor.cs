using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using DapperToolkit.Core.Interfaces;
using DapperToolkit.Core.Mapping;

namespace DapperToolkit.Core.Builders;

public sealed class PredicateVisitor<TEntity>(EntityMapping mapping, ISqlDialect dialect) : ExpressionVisitor
    where TEntity : class
{
    private readonly StringBuilder _sb = new();
    private readonly Dictionary<string, object> _parameters = [];
    private readonly ISqlDialect _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
    private readonly EntityMapping _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
    private int _index = 0;

    public (string Sql, object Parameters) Translate(Expression<Func<TEntity, bool>> predicate)
    {
        var body = predicate.Body;

        // x => x.IsActive  (bare boolean property)
        if (body is MemberExpression m && IsEntityBooleanMember(m))
        {
            var column = GetColumnNameForMember(m);
            _sb.Append($"{column} = {_dialect.FormatBoolean(true)}");
        }
        // x => !x.IsActive
        else if (body is UnaryExpression u &&
             u.NodeType == ExpressionType.Not &&
             u.Operand is MemberExpression m2 &&
             IsEntityBooleanMember(m2))
        {
            var column = GetColumnNameForMember(m2);
            _sb.Append($"{column} = {_dialect.FormatBoolean(false)}");
        }
        else
        {
            Visit(body);
        }

        var anon = _parameters.ToDictionary(k => k.Key, v => v.Value);
        return (_sb.ToString(), anon);
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        // Boolean comparisons
        if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
        {
            if (IsBooleanComparison(node, out var memberExpr, out var value))
            {
                var column = GetColumnNameForMember(memberExpr);
                var normalized = _dialect.FormatBoolean(value);
                _sb.Append($"({column} = {normalized})");
                return node;
            }

            // NULL comparisons
            if (IsNullConstant(node.Right))
            {
                _sb.Append("(");
                Visit(node.Left);
                _sb.Append(node.NodeType == ExpressionType.Equal ? " IS NULL)" : " IS NOT NULL)");
                return node;
            }

            if (IsNullConstant(node.Left))
            {
                _sb.Append("(");
                Visit(node.Right);
                _sb.Append(node.NodeType == ExpressionType.Equal ? " IS NULL)" : " IS NOT NULL)");
                return node;
            }
        }

        _sb.Append("(");
        Visit(node.Left);

        _sb.Append(node.NodeType switch
        {
            ExpressionType.Equal => " = ",
            ExpressionType.NotEqual => " <> ",
            ExpressionType.GreaterThan => " > ",
            ExpressionType.GreaterThanOrEqual => " >= ",
            ExpressionType.LessThan => " < ",
            ExpressionType.LessThanOrEqual => " <= ",
            ExpressionType.AndAlso => " AND ",
            ExpressionType.OrElse => " OR ",
            _ => throw new NotSupportedException($"Unsupported node: {node.NodeType}")
        });

        Visit(node.Right);
        _sb.Append(")");
        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        // Entity property (x.Prop)
        if (typeof(TEntity).IsAssignableFrom(node.Expression?.Type ?? typeof(object)))
        {
            var prop = node.Member as PropertyInfo
                       ?? throw new NotSupportedException($"Member '{node.Member.Name}' is not a property.");

            var propMap = _mapping.PropertyMappings
                .FirstOrDefault(pm => pm.Property == prop)
                ?? throw new InvalidOperationException($"No mapping found for property '{prop.Name}'.");

            var column = _dialect.QuoteIdentifier(propMap.ColumnName);
            _sb.Append(column);
            return node;
        }

        // Closure captured variable
        if (node.Expression is ConstantExpression closure)
        {
            var value = GetValueFromClosure(closure.Value, node.Member);

            if (value is bool b)
            {
                AppendBooleanPredicateLiteral(b);
                return node;
            }

            if (value is null)
            {
                _sb.Append("NULL");
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
            AppendBooleanPredicateLiteral(b);
            return node;
        }

        if (node.Value is null)
        {
            _sb.Append("NULL");
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
            VisitMember(memberExpr);
            _sb.Append(" LIKE ");

            var arg = node.Arguments[0];
            var value = EvaluateExpression(arg);
            AppendParameter($"%{value}%");
            return node;
        }

        throw new NotSupportedException($"Method call '{node.Method.Name}' is not supported.");
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        if (node.NodeType == ExpressionType.Not)
        {
            _sb.Append("NOT (");
            Visit(node.Operand);
            _sb.Append(")");
            return node;
        }

        return base.VisitUnary(node);
    }

    private void AppendBooleanPredicateLiteral(bool value)
    {
        // Universal SQL safe literal: TRUE → 1=1, FALSE → 1=0
        _sb.Append(value ? "1=1" : "1=0");
    }

    private bool IsBooleanComparison(BinaryExpression node, out MemberExpression member, out bool value)
    {
        if (node.Left is MemberExpression ml && IsEntityBooleanMember(ml) &&
            TryEvalToBool(node.Right, out value))
        {
            member = ml;
            return true;
        }

        if (node.Right is MemberExpression mr && IsEntityBooleanMember(mr) &&
            TryEvalToBool(node.Left, out value))
        {
            member = mr;
            return true;
        }

        member = null!;
        value = false;
        return false;
    }

    private bool TryEvalToBool(Expression expr, out bool value)
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
        if (!typeof(TEntity).IsAssignableFrom(node.Expression?.Type ?? typeof(object)))
            return false;

        if (node.Member is not PropertyInfo pi) return false;

        var t = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType;
        return t == typeof(bool);
    }

    private string GetColumnNameForMember(MemberExpression node)
    {
        var prop = (PropertyInfo)node.Member;
        var map = _mapping.PropertyMappings.First(pm => pm.Property == prop);
        return _dialect.QuoteIdentifier(map.ColumnName);
    }

    private static bool IsNullConstant(Expression expr)
    {
        if (expr is ConstantExpression c)
            return c.Value is null;

        if (expr is MemberExpression m && m.Expression is ConstantExpression closure)
        {
            var value = GetValueFromClosure(closure.Value, m.Member);
            return value is null;
        }

        var evaluated = EvaluateExpression(expr);
        return evaluated is null;
    }

    private void AppendParameter(object? value)
    {
        var paramKey = $"p{_index++}";
        _parameters[paramKey] = value ?? DBNull.Value;

        var sqlParamName = _dialect.FormatParameter(paramKey);
        _sb.Append(sqlParamName);
    }

    private static object? GetValueFromClosure(object? closureObject, MemberInfo member)
    {
        if (closureObject is null)
            return null;

        return member switch
        {
            FieldInfo fi => fi.GetValue(closureObject),
            PropertyInfo pi => pi.GetValue(closureObject),
            _ => throw new NotSupportedException($"Unsupported closure member type: {member.MemberType}")
        };
    }

    private static object? EvaluateExpression(Expression expr)
    {
        if (expr is ConstantExpression c)
            return c.Value;

        var lambda = Expression.Lambda(expr);
        var compiled = lambda.Compile();
        return compiled.DynamicInvoke();
    }
}
