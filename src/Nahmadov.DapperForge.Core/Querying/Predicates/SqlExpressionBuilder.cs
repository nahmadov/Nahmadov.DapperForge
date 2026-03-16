using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;

namespace Nahmadov.DapperForge.Core.Querying.Predicates;
/// <summary>
/// Handles SQL generation for predicate expressions including columns, parameters, and operators.
/// Configurable table alias and parameter prefix allow reuse across root queries and include filters.
/// </summary>
internal sealed class SqlExpressionBuilder
{
    private readonly ISqlDialect _dialect;
    private readonly Dictionary<PropertyInfo, PropertyMapping> _propertyLookup;
    private readonly StringBuilder _sql;
    private readonly Dictionary<string, object?> _parameters;
    private readonly string _alias;
    private readonly string _paramPrefix;
    private int _paramIndex;

    public SqlExpressionBuilder(
        ISqlDialect dialect,
        Dictionary<PropertyInfo, PropertyMapping> propertyLookup,
        StringBuilder sql,
        Dictionary<string, object?> parameters,
        string alias = "a",
        string paramPrefix = "p")
    {
        _dialect = dialect;
        _propertyLookup = propertyLookup;
        _sql = sql;
        _parameters = parameters;
        _alias = alias;
        _paramPrefix = paramPrefix;
    }

    public ISqlDialect Dialect => _dialect;

    public void ResetParameterIndex() => _paramIndex = 0;

    // ── Entity property detection ────────────────────────────────────────────

    /// <summary>Returns true when the member expression refers to a mapped entity property.</summary>
    public bool IsEntityProperty(MemberExpression node)
        => node.Expression is ParameterExpression
           && node.Member is PropertyInfo prop
           && _propertyLookup.ContainsKey(prop);

    /// <summary>Returns true when the entity property is of type <see cref="string"/>.</summary>
    public bool IsEntityStringProperty(MemberExpression node)
        => IsEntityProperty(node)
           && ((PropertyInfo)node.Member).PropertyType == typeof(string);

    /// <summary>Returns true when the entity property is of type <see cref="bool"/> (including nullable).</summary>
    public bool IsEntityBoolProperty(MemberExpression node)
    {
        if (!IsEntityProperty(node)) return false;
        var underlying = Nullable.GetUnderlyingType(((PropertyInfo)node.Member).PropertyType)
                         ?? ((PropertyInfo)node.Member).PropertyType;
        return underlying == typeof(bool);
    }

    // ── Column helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns the fully qualified column reference (e.g. <c>a."Col"</c>) for a member expression.
    /// </summary>
    public string GetColumnNameForMember(MemberExpression node)
    {
        var prop = (PropertyInfo)node.Member;
        if (_propertyLookup.TryGetValue(prop, out var map))
            return $"{_alias}.{_dialect.QuoteIdentifier(map.ColumnName)}";

        throw new InvalidOperationException($"No mapping found for property '{prop.Name}'.");
    }

    /// <summary>
    /// Appends the column reference, automatically expanding boolean properties to <c>col = 1</c>
    /// when used as a standalone projection (e.g. <c>x => x.IsActive</c>).
    /// </summary>
    public void AppendColumn(PropertyInfo property)
    {
        if (!_propertyLookup.TryGetValue(property, out var map))
            throw new InvalidOperationException($"No mapping found for property '{property.Name}'.");

        var column = $"{_alias}.{_dialect.QuoteIdentifier(map.ColumnName)}";
        var propType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (propType == typeof(bool))
            _sql.Append($"{column} = {_dialect.FormatBoolean(true)}");
        else
            _sql.Append(column);
    }

    // ── Parameter helpers ────────────────────────────────────────────────────

    public void AppendParameter(object? value)
        => _sql.Append(AddParameter(value));

    public string AddParameter(object? value)
    {
        var paramKey = $"{_paramPrefix}{_paramIndex++}";
        _parameters[paramKey] = value ?? DBNull.Value;
        return _dialect.FormatParameter(paramKey);
    }

    // ── Literal helpers ──────────────────────────────────────────────────────

    public void AppendBooleanLiteral(bool value)
        => _sql.Append(value ? "1=1" : "1=0");

    public void AppendSql(string sql) => _sql.Append(sql);
    public void AppendSql(char c) => _sql.Append(c);

    // ── Operators ────────────────────────────────────────────────────────────

    public static string GetSqlOperator(ExpressionType nodeType) => nodeType switch
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
}
