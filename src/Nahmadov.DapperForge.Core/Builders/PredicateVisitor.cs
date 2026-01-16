using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Nahmadov.DapperForge.Core.Builders.Predicate;
using Nahmadov.DapperForge.Core.Interfaces;
using Nahmadov.DapperForge.Core.Mapping;

namespace Nahmadov.DapperForge.Core.Builders;

/// <summary>
/// Translates LINQ expression trees into SQL predicates and parameter objects.
/// Supports common comparison operators, string methods, logical operators, and collection Contains (IN clause).
/// </summary>
/// <typeparam name="TEntity">
/// The entity type being queried. Must be a reference type (class constraint) to ensure
/// member expressions can be properly resolved and to support null comparisons.
/// </typeparam>
/// <remarks>
/// <para><b>Performance Optimizations:</b></para>
/// <list type="bullet">
/// <item>
/// <b>Expression Compilation Caching:</b> Compiled expressions are cached in a thread-safe LRU cache
/// (max 1000 entries). Cache key is based on structural hashing of the expression tree for reliability.
/// </item>
/// <item>
/// <b>SQL Parameterization:</b> All values are parameterized to prevent SQL injection and enable database query plan caching.
/// </item>
/// </list>
/// <para><b>Supported Expressions:</b></para>
/// <list type="bullet">
/// <item>Comparisons: ==, !=, &gt;, &gt;=, &lt;, &lt;=</item>
/// <item>Logical: &amp;&amp; (AND), || (OR), ! (NOT)</item>
/// <item>String methods: Contains, StartsWith, EndsWith (with optional case-insensitive mode)</item>
/// <item>Null checks: prop == null, prop != null</item>
/// <item>Boolean properties: prop, !prop, prop == true/false</item>
/// <item>Collection Contains: list.Contains(prop) -&gt; IN clause</item>
/// </list>
/// </remarks>
public sealed class PredicateVisitor<TEntity> : ExpressionVisitor
    where TEntity : class
{
    private readonly EntityMapping _mapping;
    private readonly Dictionary<PropertyInfo, PropertyMapping> _propertyLookup;

    private readonly StringBuilder _sql = new();
    private readonly Dictionary<string, object?> _parameters = [];

    private readonly SqlExpressionBuilder _sqlBuilder;
    private readonly BooleanExpressionHandler<TEntity> _booleanHandler;
    private readonly NullExpressionHandler _nullHandler;
    private readonly StringExpressionHandler<TEntity> _stringHandler;
    private readonly CollectionExpressionHandler<TEntity> _collectionHandler;

    private bool _ignoreCase;
    private readonly bool _defaultIgnoreCase;
    private readonly bool _treatEmptyStringAsNull;

    /// <summary>
    /// Initializes a new predicate visitor for the given mapping and dialect.
    /// </summary>
    /// <param name="mapping">Entity mapping metadata.</param>
    /// <param name="dialect">SQL dialect used for identifier and parameter formatting.</param>
    public PredicateVisitor(EntityMapping mapping, ISqlDialect dialect)
    {
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        ArgumentNullException.ThrowIfNull(dialect);

        _propertyLookup = _mapping.PropertyMappings.ToDictionary(pm => pm.Property, pm => pm);
        _defaultIgnoreCase = string.Equals(dialect.Name, "Oracle", StringComparison.OrdinalIgnoreCase);
        _treatEmptyStringAsNull = string.Equals(dialect.Name, "Oracle", StringComparison.OrdinalIgnoreCase);

        _sqlBuilder = new SqlExpressionBuilder(dialect, _propertyLookup, _sql, _parameters);
        _booleanHandler = new BooleanExpressionHandler<TEntity>(_sqlBuilder);
        _nullHandler = new NullExpressionHandler(_sqlBuilder, _treatEmptyStringAsNull, Visit);
        _stringHandler = new StringExpressionHandler<TEntity>(_sqlBuilder, _nullHandler, () => _ignoreCase);
        _collectionHandler = new CollectionExpressionHandler<TEntity>(_sqlBuilder);
    }

    /// <summary>
    /// Translates a boolean predicate expression into SQL WHERE clause and parameters.
    /// </summary>
    /// <param name="predicate">
    /// Expression to translate into SQL. Must be a boolean predicate (returns bool).
    /// </param>
    /// <param name="ignoreCase">
    /// Optional override for case-insensitive comparisons. When null, defaults based on dialect:
    /// Oracle defaults to true (case-sensitive by default), SQL Server defaults to false (collation-dependent).
    /// When true, wraps string comparisons with LOWER() on both sides.
    /// </param>
    /// <returns>
    /// Tuple containing:
    /// <list type="bullet">
    /// <item>Sql: WHERE clause SQL (without "WHERE" keyword)</item>
    /// <item>Parameters: Dictionary of parameter names to values</item>
    /// </list>
    /// </returns>
    public (string Sql, object Parameters) Translate(Expression<Func<TEntity, bool>> predicate, bool? ignoreCase = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        _sql.Clear();
        _parameters.Clear();
        _sqlBuilder.ResetParameterIndex();
        _ignoreCase = ignoreCase ?? _defaultIgnoreCase;

        if (!_booleanHandler.TryHandleBooleanProjection(predicate.Body))
        {
            Visit(predicate.Body);
        }

        return (_sql.ToString(), new Dictionary<string, object?>(_parameters));
    }

    /// <summary>
    /// Visits binary expressions and emits SQL operators or specialized comparisons.
    /// </summary>
    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (_booleanHandler.TryHandleBooleanComparison(node))
            return node;

        if (_nullHandler.TryHandleNullComparison(node))
            return node;

        if (_stringHandler.TryHandleStringEquality(node))
            return node;

        _sqlBuilder.AppendSql('(');
        Visit(node.Left);
        _sqlBuilder.AppendSql(SqlExpressionBuilder.GetSqlOperator(node.NodeType));
        Visit(node.Right);
        _sqlBuilder.AppendSql(')');
        return node;
    }

    /// <summary>
    /// Visits member access expressions to translate entity properties or closure values.
    /// </summary>
    protected override Expression VisitMember(MemberExpression node)
    {
        if (EntityPropertyHelper.IsEntityProperty<TEntity>(node))
        {
            _sqlBuilder.AppendColumn((PropertyInfo)node.Member);
            return node;
        }

        if (node.Expression is ConstantExpression closure)
        {
            var value = ExpressionEvaluator.GetValueFromClosure(closure.Value, node.Member);

            if (value is bool b)
            {
                _sqlBuilder.AppendBooleanLiteral(b);
                return node;
            }

            if (value is null)
            {
                _sqlBuilder.AppendSql("NULL");
                return node;
            }

            _sqlBuilder.AppendParameter(value);
            return node;
        }

        return base.VisitMember(node);
    }

    /// <summary>
    /// Visits constant expressions and emits literal SQL or parameters.
    /// </summary>
    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value is bool b)
        {
            _sqlBuilder.AppendBooleanLiteral(b);
            return node;
        }

        if (node.Value is null)
        {
            _sqlBuilder.AppendSql("NULL");
            return node;
        }

        _sqlBuilder.AppendParameter(node.Value);
        return node;
    }

    /// <summary>
    /// Visits method calls to translate supported string operations.
    /// </summary>
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.Name == nameof(string.Contains) &&
            node.Object is MemberExpression memberExpr &&
            memberExpr.Expression is ParameterExpression)
        {
            _stringHandler.AppendLikeContains(memberExpr, node.Arguments[0]);
            return node;
        }

        if (node.Method.Name == nameof(string.StartsWith) &&
            node.Object is MemberExpression startsExpr &&
            startsExpr.Expression is ParameterExpression)
        {
            _stringHandler.AppendLikeStartsWith(startsExpr, node.Arguments[0]);
            return node;
        }

        if (node.Method.Name == nameof(string.EndsWith) &&
            node.Object is MemberExpression endsExpr &&
            endsExpr.Expression is ParameterExpression)
        {
            _stringHandler.AppendLikeEndsWith(endsExpr, node.Arguments[0]);
            return node;
        }

        if (_collectionHandler.IsEnumerableContains(node, out var memberExprForIn, out var valuesExpr))
        {
            _collectionHandler.AppendInClause(memberExprForIn, valuesExpr);
            return node;
        }

        throw new NotSupportedException($"Method call '{node.Method.Name}' is not supported.");
    }

    /// <summary>
    /// Visits unary expressions such as logical NOT.
    /// </summary>
    protected override Expression VisitUnary(UnaryExpression node)
    {
        if (node.NodeType == ExpressionType.Not)
        {
            _sqlBuilder.AppendSql("NOT (");
            Visit(node.Operand);
            _sqlBuilder.AppendSql(')');
            return node;
        }

        return base.VisitUnary(node);
    }
}
