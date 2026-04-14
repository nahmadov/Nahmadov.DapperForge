using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;

namespace Nahmadov.DapperForge.Core.Querying.Predicates;

/// <summary>
/// The single, unified expression-to-SQL translator for all predicate contexts
/// (root WHERE clauses, Include join conditions, split-query filter additions).
/// </summary>
/// <remarks>
/// Replaces both <c>PredicateVisitor&lt;TEntity&gt;</c> (which is now a thin typed wrapper)
/// and the deleted <c>IncludeFilterTranslator</c>.
/// <para>
/// Supply a different <paramref name="alias"/> and <paramref name="paramPrefix"/> when translating
/// Include filters to avoid parameter-name collisions with the root query.
/// </para>
/// <para><b>Supported expressions:</b></para>
/// <list type="bullet">
/// <item>Comparisons: ==, !=, &gt;, &gt;=, &lt;, &lt;=</item>
/// <item>Logical: &amp;&amp; (AND), || (OR), ! (NOT)</item>
/// <item>String methods: Contains, StartsWith, EndsWith (with optional case-insensitive mode)</item>
/// <item>Null checks: prop == null, prop != null, Nullable.HasValue</item>
/// <item>Boolean properties: prop, !prop, prop == true/false</item>
/// <item>Collection Contains: list.Contains(prop) → IN clause</item>
/// </list>
/// <para><b>Extensibility:</b></para>
/// <para>
/// Dialect packages can subclass this translator and override <see cref="VisitMethodCall"/>
/// to add dialect-specific function support (e.g. SQLite's <c>date()</c> / <c>datetime()</c>).
/// Use the protected helper methods (<see cref="IsEntityProperty"/>, <see cref="GetColumnNameForMember"/>,
/// <see cref="AppendSql(string)"/>, <see cref="AddParameter"/>) to emit SQL and parameters
/// without exposing the internal <c>SqlExpressionBuilder</c>.
/// </para>
/// </remarks>
public class SqlPredicateTranslator : ExpressionVisitor
{
    private readonly bool _defaultIgnoreCase;
    private readonly bool _treatEmptyStringAsNull;

    private readonly StringBuilder _sql = new();
    private readonly Dictionary<string, object?> _parameters = [];

    private readonly SqlExpressionBuilder _sqlBuilder;
    private readonly BooleanExpressionHandler _booleanHandler;
    private readonly NullExpressionHandler _nullHandler;
    private readonly StringExpressionHandler _stringHandler;
    private readonly CollectionExpressionHandler _collectionHandler;

    private bool _ignoreCase;

    /// <param name="mapping">Entity mapping for the type whose properties appear in the expression.</param>
    /// <param name="dialect">SQL dialect for identifier quoting and parameter formatting.</param>
    /// <param name="alias">Table alias used when qualifying column references. Defaults to <c>"a"</c>.</param>
    /// <param name="paramPrefix">
    /// Prefix for generated parameter names (e.g. <c>"p"</c> → <c>@p0</c>, <c>@p1</c>…).
    /// Use a unique prefix per include node to avoid collisions with root-query parameters.
    /// </param>
    public SqlPredicateTranslator(
        EntityMapping mapping,
        ISqlDialect dialect,
        string alias = "a",
        string paramPrefix = "p")
    {
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(dialect);

        _defaultIgnoreCase = string.Equals(dialect.Name, "Oracle", StringComparison.OrdinalIgnoreCase);
        _treatEmptyStringAsNull = string.Equals(dialect.Name, "Oracle", StringComparison.OrdinalIgnoreCase);

        var propertyLookup = mapping.PropertyMappings.ToDictionary(
            pm => pm.Property,
            pm => pm,
            PropertyInfoEqualityComparer.Instance);

        _sqlBuilder = new SqlExpressionBuilder(dialect, propertyLookup, _sql, _parameters, alias, paramPrefix);
        _booleanHandler = new BooleanExpressionHandler(_sqlBuilder);
        _nullHandler = new NullExpressionHandler(_sqlBuilder, _treatEmptyStringAsNull, Visit);
        _stringHandler = new StringExpressionHandler(_sqlBuilder, _nullHandler, () => _ignoreCase);
        _collectionHandler = new CollectionExpressionHandler(_sqlBuilder);
    }

    /// <summary>
    /// Translates <paramref name="predicate"/> into a SQL condition string and a parameter bag.
    /// </summary>
    /// <param name="predicate">Any boolean lambda (typed or untyped).</param>
    /// <param name="ignoreCase">
    /// Override case-sensitivity. When <c>null</c> the dialect default is used
    /// (Oracle defaults to case-sensitive; SQL Server/SQLite default to case-insensitive).
    /// </param>
    public (string Sql, IReadOnlyDictionary<string, object?> Parameters) Translate(
        LambdaExpression predicate,
        bool? ignoreCase = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        _sql.Clear();
        _parameters.Clear();
        _sqlBuilder.ResetParameterIndex();
        _ignoreCase = ignoreCase ?? _defaultIgnoreCase;

        if (!_booleanHandler.TryHandleBooleanProjection(predicate.Body))
            Visit(predicate.Body);

        return (_sql.ToString(), new Dictionary<string, object?>(_parameters));
    }

    // ── Visitors ─────────────────────────────────────────────────────────────

    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (_booleanHandler.TryHandleBooleanComparison(node)) return node;
        if (_nullHandler.TryHandleNullComparison(node)) return node;
        if (_stringHandler.TryHandleStringEquality(node)) return node;

        _sqlBuilder.AppendSql('(');
        Visit(node.Left);
        _sqlBuilder.AppendSql(SqlExpressionBuilder.GetSqlOperator(node.NodeType));
        Visit(node.Right);
        _sqlBuilder.AppendSql(')');
        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        // Nullable<T>.HasValue → col IS NOT NULL
        if (node.Member.Name == "HasValue"
            && node.Expression is MemberExpression innerHasValue
            && _sqlBuilder.IsEntityProperty(innerHasValue))
        {
            _sqlBuilder.AppendSql(_sqlBuilder.GetColumnNameForMember(innerHasValue));
            _sqlBuilder.AppendSql(" IS NOT NULL");
            return node;
        }

        // Nullable<T>.Value → just the column
        if (node.Member.Name == "Value"
            && node.Expression is MemberExpression innerValue
            && _sqlBuilder.IsEntityProperty(innerValue))
        {
            return Visit(innerValue);
        }

        if (_sqlBuilder.IsEntityProperty(node))
        {
            _sqlBuilder.AppendColumn((PropertyInfo)node.Member);
            return node;
        }

        if (IsClosureBasedExpression(node))
        {
            var value = ExpressionEvaluator.Evaluate(node);
            if (value is bool b) { _sqlBuilder.AppendBooleanLiteral(b); return node; }
            if (value is null) { _sqlBuilder.AppendSql("NULL"); return node; }
            _sqlBuilder.AppendParameter(value);
            return node;
        }

        return base.VisitMember(node);
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value is bool b) { _sqlBuilder.AppendBooleanLiteral(b); return node; }
        if (node.Value is null) { _sqlBuilder.AppendSql("NULL"); return node; }
        _sqlBuilder.AppendParameter(node.Value);
        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.Name == nameof(string.Contains)
            && node.Object is MemberExpression memberContains
            && _sqlBuilder.IsEntityProperty(memberContains))
        {
            _stringHandler.AppendLikeContains(memberContains, node.Arguments[0]);
            return node;
        }

        if (node.Method.Name == nameof(string.StartsWith)
            && node.Object is MemberExpression memberStarts
            && _sqlBuilder.IsEntityProperty(memberStarts))
        {
            _stringHandler.AppendLikeStartsWith(memberStarts, node.Arguments[0]);
            return node;
        }

        if (node.Method.Name == nameof(string.EndsWith)
            && node.Object is MemberExpression memberEnds
            && _sqlBuilder.IsEntityProperty(memberEnds))
        {
            _stringHandler.AppendLikeEndsWith(memberEnds, node.Arguments[0]);
            return node;
        }

        if (_collectionHandler.IsEnumerableContains(node, out var memberForIn, out var valuesExpr))
        {
            _collectionHandler.AppendInClause(memberForIn, valuesExpr);
            return node;
        }

        throw new NotSupportedException($"Method call '{node.Method.Name}' is not supported.");
    }

    // ── Protected helpers for subclasses ──────────────────────────────────────

    /// <summary>Returns <c>true</c> when the member expression refers to a mapped entity property.</summary>
    protected bool IsEntityProperty(MemberExpression node) => _sqlBuilder.IsEntityProperty(node);

    /// <summary>Returns the fully-qualified column reference (e.g. <c>a."Col"</c>) for a member expression.</summary>
    protected string GetColumnNameForMember(MemberExpression node) => _sqlBuilder.GetColumnNameForMember(node);

    /// <summary>Appends a raw SQL fragment to the output buffer.</summary>
    protected void AppendSql(string sql) => _sqlBuilder.AppendSql(sql);

    /// <summary>Adds a parameter to the bag and returns the formatted placeholder (e.g. <c>@p0</c>).</summary>
    protected string AddParameter(object? value) => _sqlBuilder.AddParameter(value);

    protected override Expression VisitUnary(UnaryExpression node)
    {
        if (node.NodeType == ExpressionType.Not)
        {
            _sqlBuilder.AppendSql("NOT (");
            Visit(node.Operand);
            _sqlBuilder.AppendSql(')');
            return node;
        }

        if (node.NodeType == ExpressionType.Convert)
            return Visit(node.Operand);

        return base.VisitUnary(node);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsClosureBasedExpression(Expression? expr)
    {
        while (expr is MemberExpression member) expr = member.Expression;
        return expr is ConstantExpression;
    }
}
