using System.Linq.Expressions;
using System.Text;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;

namespace Nahmadov.DapperForge.Core.Querying.Predicates;
/// <summary>
/// Translates LINQ ordering expressions into SQL ORDER BY clauses.
/// </summary>
public sealed class OrderingVisitor<TEntity> : ExpressionVisitor
    where TEntity : class
{
    private readonly EntityMapping _mapping;
    private readonly ISqlDialect _dialect;
    private readonly Dictionary<System.Reflection.PropertyInfo, PropertyMapping> _propertyLookup;
    private readonly StringBuilder _orderBy = new();

    /// <summary>
    /// Initializes a new ordering visitor for the given mapping and dialect.
    /// </summary>
    /// <param name="mapping">Entity mapping metadata.</param>
    /// <param name="dialect">SQL dialect used for identifier formatting.</param>
    public OrderingVisitor(EntityMapping mapping, ISqlDialect dialect)
    {
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        _propertyLookup = _mapping.PropertyMappings.ToDictionary(pm => pm.Property, pm => pm);
    }

    /// <summary>
    /// Translates an ordering expression into SQL ORDER BY clause.
    /// </summary>
    /// <param name="expression">Ordering expression (e.g., x => x.Name).</param>
    /// <param name="isDescending">Whether to sort in descending order.</param>
    /// <returns>ORDER BY clause text or empty string if expression cannot be resolved.</returns>
    public string Translate(Expression<Func<TEntity, object?>> expression, bool isDescending = false)
    {
        ArgumentNullException.ThrowIfNull(expression);

        _orderBy.Clear();

        var propertyInfo = ExtractPropertyInfo(expression.Body);
        if (propertyInfo is null)
        {
            return string.Empty;
        }

        return BuildOrderByClause(propertyInfo, isDescending);
    }

    /// <summary>
    /// Appends an ordering expression to the current ORDER BY clause.
    /// </summary>
    /// <param name="expression">Ordering expression (e.g., x => x.Name).</param>
    /// <param name="isDescending">Whether to sort in descending order.</param>
    /// <returns>Updated ORDER BY clause text.</returns>
    public string ThenBy(Expression<Func<TEntity, object?>> expression, bool isDescending = false)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var propertyInfo = ExtractPropertyInfo(expression.Body);
        if (propertyInfo is null)
        {
            return _orderBy.ToString();
        }

        if (_orderBy.Length > 0)
        {
            _orderBy.Append(", ");
        }

        AppendPropertyToOrderBy(propertyInfo, isDescending);

        return _orderBy.ToString();
    }

    /// <summary>
    /// Gets the complete ORDER BY clause.
    /// </summary>
    /// <returns>ORDER BY clause or empty string if no expressions are translated.</returns>
    public string GetOrderBySql()
    {
        return _orderBy.ToString();
    }

    /// <summary>
    /// Extracts the property information from an ordering expression body.
    /// </summary>
    /// <param name="body">Expression body to extract from.</param>
    /// <returns>PropertyInfo if extracted, null otherwise.</returns>
    private System.Reflection.PropertyInfo? ExtractPropertyInfo(Expression body)
    {
        // Handle: x => x.Property
        if (body is MemberExpression member && member.Expression is ParameterExpression)
        {
            return member.Member as System.Reflection.PropertyInfo;
        }

        // Handle: x => (object)x.Property (cast to object)
        if (body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
        {
            if (unary.Operand is MemberExpression innerMember && innerMember.Expression is ParameterExpression)
            {
                return innerMember.Member as System.Reflection.PropertyInfo;
            }
        }

        return null;
    }

    /// <summary>
    /// Builds an ORDER BY clause for a single property.
    /// </summary>
    /// <param name="propertyInfo">Property to order by.</param>
    /// <param name="isDescending">Whether to sort descending.</param>
    /// <returns>ORDER BY clause text.</returns>
    private string BuildOrderByClause(System.Reflection.PropertyInfo propertyInfo, bool isDescending)
    {
        _orderBy.Clear();
        AppendPropertyToOrderBy(propertyInfo, isDescending);
        return _orderBy.ToString();
    }

    /// <summary>
    /// Appends a property column reference to the ORDER BY clause with table alias.
    /// </summary>
    /// <param name="propertyInfo">Property to append.</param>
    /// <param name="isDescending">Whether to sort descending.</param>
    private void AppendPropertyToOrderBy(System.Reflection.PropertyInfo propertyInfo, bool isDescending)
    {
        if (!_propertyLookup.TryGetValue(propertyInfo, out var mapping))
        {
            throw new InvalidOperationException(
                $"Property '{propertyInfo.Name}' of entity '{typeof(TEntity).Name}' is not mapped to a column.");
        }

        _orderBy.Append($"a.{_dialect.QuoteIdentifier(mapping.ColumnName)}");
        if (isDescending)
        {
            _orderBy.Append(" DESC");
        }
    }
}


