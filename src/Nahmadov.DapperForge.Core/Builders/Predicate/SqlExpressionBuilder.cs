using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Nahmadov.DapperForge.Core.Interfaces;
using Nahmadov.DapperForge.Core.Mapping;

namespace Nahmadov.DapperForge.Core.Builders.Predicate;

/// <summary>
/// Handles SQL generation for predicate expressions including columns, parameters, and operators.
/// </summary>
internal sealed class SqlExpressionBuilder
{
    private readonly ISqlDialect _dialect;
    private readonly Dictionary<PropertyInfo, PropertyMapping> _propertyLookup;
    private readonly StringBuilder _sql;
    private readonly Dictionary<string, object?> _parameters;
    private int _paramIndex;

    public SqlExpressionBuilder(
        ISqlDialect dialect,
        Dictionary<PropertyInfo, PropertyMapping> propertyLookup,
        StringBuilder sql,
        Dictionary<string, object?> parameters)
    {
        _dialect = dialect;
        _propertyLookup = propertyLookup;
        _sql = sql;
        _parameters = parameters;
    }

    public ISqlDialect Dialect => _dialect;

    public void ResetParameterIndex() => _paramIndex = 0;

    public string GetColumnNameForMember(MemberExpression node)
    {
        var prop = (PropertyInfo)node.Member;
        if (_propertyLookup.TryGetValue(prop, out var map))
            return $"a.{_dialect.QuoteIdentifier(map.ColumnName)}";

        throw new InvalidOperationException($"No mapping found for property '{prop.Name}'.");
    }

    public void AppendColumn(PropertyInfo property)
    {
        if (!_propertyLookup.TryGetValue(property, out var map))
            throw new InvalidOperationException($"No mapping found for property '{property.Name}'.");

        var column = $"a.{_dialect.QuoteIdentifier(map.ColumnName)}";
        var propType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (propType == typeof(bool))
        {
            _sql.Append($"{column} = {_dialect.FormatBoolean(true)}");
        }
        else
        {
            _sql.Append(column);
        }
    }

    public void AppendParameter(object? value)
    {
        var paramSql = AddParameter(value);
        _sql.Append(paramSql);
    }

    public string AddParameter(object? value)
    {
        var paramKey = $"p{_paramIndex++}";
        _parameters[paramKey] = value ?? DBNull.Value;
        return _dialect.FormatParameter(paramKey);
    }

    public void AppendBooleanLiteral(bool value)
    {
        _sql.Append(value ? "1=1" : "1=0");
    }

    public void AppendSql(string sql) => _sql.Append(sql);

    public void AppendSql(char c) => _sql.Append(c);

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
