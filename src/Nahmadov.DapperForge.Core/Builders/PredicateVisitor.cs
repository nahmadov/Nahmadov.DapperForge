using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

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
/// <b>Expression Compilation Caching:</b> Compiled expressions are cached in a static ConcurrentDictionary
/// (max 1000 entries). Cache key is based on expression's ToString() representation.
/// Subsequent calls with the same expression structure reuse cached compiled delegates, avoiding recompilation overhead.
/// </item>
/// <item>
/// <b>SQL Parameterization:</b> All values are parameterized to prevent SQL injection and enable database query plan caching.
/// </item>
/// <item>
/// <b>Cache Eviction:</b> When cache reaches 1000 entries, entire cache is cleared (simple eviction strategy).
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
    private static readonly ConcurrentDictionary<ExpressionCacheKey, Delegate> _compiledExpressionCache = new();
    private const int MaxCacheSize = 1000;

    private readonly EntityMapping _mapping;
    private readonly ISqlDialect _dialect;
    private readonly Dictionary<PropertyInfo, PropertyMapping> _propertyLookup;

    private readonly StringBuilder _sql = new();
    private readonly Dictionary<string, object?> _parameters = [];
    private int _paramIndex;
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
        _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        _propertyLookup = _mapping.PropertyMappings.ToDictionary(pm => pm.Property, pm => pm);
        _defaultIgnoreCase = string.Equals(_dialect.Name, "Oracle", StringComparison.OrdinalIgnoreCase);
        _treatEmptyStringAsNull = string.Equals(_dialect.Name, "Oracle", StringComparison.OrdinalIgnoreCase);
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
    /// <exception cref="ArgumentNullException">Thrown when predicate is null.</exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when expression contains unsupported operations (e.g., method calls other than supported string methods).
    /// </exception>
    /// <remarks>
    /// <b>Performance:</b> Expression compilation is cached. First call compiles and caches, subsequent calls reuse cached delegate.
    /// All values are parameterized (e.g., @p0, @p1 for SQL Server; :p0, :p1 for Oracle).
    /// </remarks>
    public (string Sql, object Parameters) Translate(Expression<Func<TEntity, bool>> predicate, bool? ignoreCase = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        _sql.Clear();
        _parameters.Clear();
        _paramIndex = 0;
        _ignoreCase = ignoreCase ?? _defaultIgnoreCase;

        if (!TryHandleBooleanProjection(predicate.Body))
        {
            Visit(predicate.Body);
        }

        return (_sql.ToString(), new Dictionary<string, object?>(_parameters));
    }

    /// <summary>
    /// Visits binary expressions and emits SQL operators or specialized comparisons.
    /// </summary>
    /// <param name="node">Binary expression to translate.</param>
    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (TryHandleBooleanComparison(node))
            return node;

        if (TryHandleNullComparison(node))
            return node;

        if (TryHandleStringEquality(node))
            return node;

        _sql.Append('(');
        Visit(node.Left);
        _sql.Append(GetSqlOperator(node.NodeType));
        Visit(node.Right);
        _sql.Append(')');
        return node;
    }

    /// <summary>
    /// Visits member access expressions to translate entity properties or closure values.
    /// </summary>
    /// <param name="node">Member expression to translate.</param>
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

    /// <summary>
    /// Visits constant expressions and emits literal SQL or parameters.
    /// </summary>
    /// <param name="node">Constant expression to translate.</param>
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

    /// <summary>
    /// Visits method calls to translate supported string operations.
    /// </summary>
    /// <param name="node">Method call expression to translate.</param>
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.Name == nameof(string.Contains) &&
            node.Object is MemberExpression memberExpr &&
            memberExpr.Expression is ParameterExpression)
        {
            AppendLikeContains(memberExpr, node.Arguments[0]);
            return node;
        }

        if (node.Method.Name == nameof(string.StartsWith) &&
            node.Object is MemberExpression startsExpr &&
            startsExpr.Expression is ParameterExpression)
        {
            AppendLikeStartsWith(startsExpr, node.Arguments[0]);
            return node;
        }

        if (node.Method.Name == nameof(string.EndsWith) &&
            node.Object is MemberExpression endsExpr &&
            endsExpr.Expression is ParameterExpression)
        {
            AppendLikeEndsWith(endsExpr, node.Arguments[0]);
            return node;
        }

        if (IsEnumerableContains(node, out var memberExprForIn, out var valuesExpr))
        {
            AppendInClause(memberExprForIn, valuesExpr);
            return node;
        }

        throw new NotSupportedException($"Method call '{node.Method.Name}' is not supported.");
    }

    /// <summary>
    /// Visits unary expressions such as logical NOT.
    /// </summary>
    /// <param name="node">Unary expression to translate.</param>
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

    /// <summary>
    /// Handles predicates that consist solely of a boolean entity property (or its negation).
    /// </summary>
    /// <param name="body">Expression body to inspect.</param>
    /// <returns>True if handled directly; otherwise false.</returns>
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

    /// <summary>
    /// Handles boolean equality/inequality comparisons for entity properties.
    /// </summary>
    /// <param name="node">Binary expression to inspect.</param>
    /// <returns>True if translated; otherwise false.</returns>
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

    /// <summary>
    /// Handles comparisons against null or null-like values.
    /// </summary>
    /// <param name="node">Binary expression to inspect.</param>
    /// <returns>True if translated; otherwise false.</returns>
    private bool TryHandleNullComparison(BinaryExpression node)
    {
        if (node.NodeType is not (ExpressionType.Equal or ExpressionType.NotEqual))
            return false;

        if (IsNullLike(node.Right))
        {
            AppendNullComparison(node.Left, node.NodeType == ExpressionType.Equal);
            return true;
        }

        if (IsNullLike(node.Left))
        {
            AppendNullComparison(node.Right, node.NodeType == ExpressionType.Equal);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Appends a null or not-null check for the provided expression.
    /// </summary>
    /// <param name="expr">Expression referencing a column.</param>
    /// <param name="isEqual">True for IS NULL; false for IS NOT NULL.</param>
    private void AppendNullComparison(Expression expr, bool isEqual)
    {
        _sql.Append('(');
        Visit(expr);
        _sql.Append(isEqual ? " IS NULL)" : " IS NOT NULL)");
    }

    /// <summary>
    /// Appends a boolean equality comparison for the specified member.
    /// </summary>
    /// <param name="member">Entity property member.</param>
    /// <param name="value">Boolean literal value.</param>
    private void AppendBooleanComparison(MemberExpression member, bool value)
    {
        var column = GetColumnNameForMember(member);
        _sql.Append($"{column} = {_dialect.FormatBoolean(value)}");
    }

    /// <summary>
    /// Appends a column reference for the given property, handling bare boolean projections.
    /// </summary>
    /// <param name="property">Entity property being referenced.</param>
    private void AppendColumn(PropertyInfo property)
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

    /// <summary>
    /// Maps expression node types to SQL operators.
    /// </summary>
    /// <param name="nodeType">Expression node type.</param>
    /// <returns>SQL operator string.</returns>
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

    /// <summary>
    /// Appends a tautology or contradiction for boolean constants.
    /// </summary>
    /// <param name="value">Boolean constant value.</param>
    private void AppendBooleanLiteral(bool value)
    {
        _sql.Append(value ? "1=1" : "1=0");
    }

    /// <summary>
    /// Handles case-sensitive or insensitive string equality comparisons.
    /// </summary>
    /// <param name="node">Binary expression to inspect.</param>
    /// <returns>True if translated; otherwise false.</returns>
    private bool TryHandleStringEquality(BinaryExpression node)
    {
        if (node.NodeType is not (ExpressionType.Equal or ExpressionType.NotEqual))
            return false;

        MemberExpression? member = null;
        Expression? other = null;

        if (node.Left is MemberExpression ml && IsStringProperty(ml))
        {
            member = ml;
            other = node.Right;
        }
        else if (node.Right is MemberExpression mr && IsStringProperty(mr))
        {
            member = mr;
            other = node.Left;
        }

        if (member is null || other is null)
            return false;

        var column = GetColumnNameForMember(member);
        var left = _ignoreCase ? $"LOWER({column})" : column;

        var value = EvaluateExpression(other);
        if (IsNullLike(value))
        {
            _sql.Append($"({column} {(node.NodeType == ExpressionType.Equal ? "IS NULL" : "IS NOT NULL")})");
            return true;
        }

        var normalized = NormalizeForCase(value);
        var paramSql = AddParameter(normalized);
        var right = paramSql;
        var op = node.NodeType == ExpressionType.Equal ? "=" : "<>";
        _sql.Append($"({left} {op} {right})");
        return true;
    }

    /// <summary>
    /// Determines whether the binary expression compares a boolean entity property to a literal.
    /// </summary>
    /// <param name="node">Binary expression being inspected.</param>
    /// <param name="member">Output entity member involved in the comparison.</param>
    /// <param name="value">Output literal boolean value.</param>
    /// <returns>True when the comparison can be translated.</returns>
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

    /// <summary>
    /// Evaluates an expression to a boolean value when possible.
    /// </summary>
    /// <param name="expr">Expression to evaluate.</param>
    /// <param name="value">Resulting boolean value.</param>
    /// <returns>True when evaluation succeeds.</returns>
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

    /// <summary>
    /// Determines whether a member expression refers to a boolean entity property.
    /// </summary>
    /// <param name="node">Member expression to inspect.</param>
    /// <returns>True when the member is a boolean property on the entity.</returns>
    private bool IsEntityBooleanMember(MemberExpression node)
    {
        if (!PredicateVisitor<TEntity>.IsEntityProperty(node))
            return false;

        var propertyType = ((PropertyInfo)node.Member).PropertyType;
        var underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        return underlying == typeof(bool);
    }

    /// <summary>
    /// Determines whether a member expression refers to a string entity property.
    /// </summary>
    /// <param name="node">Member expression to inspect.</param>
    /// <returns>True when the member is a string property.</returns>
    private bool IsStringProperty(MemberExpression node)
    {
        return PredicateVisitor<TEntity>.IsEntityProperty(node) &&
               ((PropertyInfo)node.Member).PropertyType == typeof(string);
    }

    /// <summary>
    /// Checks whether a member expression targets a property on the entity type.
    /// </summary>
    /// <param name="node">Member expression to inspect.</param>
    /// <returns>True when the member is an entity property.</returns>
    private static bool IsEntityProperty(MemberExpression node)
    {
        return typeof(TEntity).IsAssignableFrom(node.Expression?.Type ?? typeof(object)) &&
               node.Member is PropertyInfo;
    }

    private bool IsEnumerableContains(MethodCallExpression node, out MemberExpression member, out Expression valuesExpr)
    {
        member = null!;
        valuesExpr = null!;

        // Pattern: list.Contains(entity.Property)
        if (node.Method.Name == nameof(Enumerable.Contains) && node.Arguments.Count == 2)
        {
            if (node.Arguments[1] is MemberExpression me && IsEntityProperty(me))
            {
                member = me;
                valuesExpr = node.Arguments[0];
                return true;
            }
        }

        if (node.Method.Name == nameof(List<int>.Contains) && node.Object is not null && node.Arguments.Count == 1)
        {
            if (node.Arguments[0] is MemberExpression me && IsEntityProperty(me))
            {
                member = me;
                valuesExpr = node.Object;
                return true;
            }
        }

        if (node.Method.Name == "Contains" && node.Object is not null && node.Arguments.Count == 1)
        {
            if (node.Arguments[0] is MemberExpression me && IsEntityProperty(me))
            {
                member = me;
                valuesExpr = node.Object;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the quoted column name with table alias for the specified member expression.
    /// </summary>
    /// <param name="node">Member expression referencing an entity property.</param>
    /// <returns>Quoted column name with table alias (e.g., "a.[ColumnName]").</returns>
    private string GetColumnNameForMember(MemberExpression node)
    {
        var prop = (PropertyInfo)node.Member;
        if (_propertyLookup.TryGetValue(prop, out var map))
            return $"a.{_dialect.QuoteIdentifier(map.ColumnName)}";

        throw new InvalidOperationException($"No mapping found for property '{prop.Name}'.");
    }

    /// <summary>
    /// Appends a LIKE predicate that matches the substring anywhere.
    /// </summary>
    /// <param name="memberExpr">Member representing the column.</param>
    /// <param name="argument">Expression producing the search value.</param>
    private void AppendLikeContains(MemberExpression memberExpr, Expression argument)
    {
        var column = GetColumnNameForMember(memberExpr);
        var raw = EvaluateExpression(argument);
        var escaped = EscapeLikeValue(raw?.ToString() ?? string.Empty);
        var normalized = NormalizeForCase($"%{escaped}%");
        var paramSql = AddParameter(normalized);

        var left = _ignoreCase ? $"LOWER({column})" : column;
        var right = paramSql;

        _sql.Append($"{left} LIKE {right} ESCAPE '\\'");
    }

    private void AppendInClause(MemberExpression memberExpr, Expression valuesExpr)
    {
        var column = GetColumnNameForMember(memberExpr);
        var rawValues = EvaluateExpression(valuesExpr) ?? throw new InvalidOperationException("IN values cannot be null.");
        if (rawValues is string)
            throw new NotSupportedException("String is not supported for IN; use a collection instead.");

        if (rawValues is not IEnumerable enumerable)
            throw new NotSupportedException("IN operator requires an IEnumerable of values.");

        var list = enumerable.Cast<object?>().ToList();
        if (list.Count == 0)
        {
            _sql.Append("1=0");
            return;
        }

        var paramSql = AddParameter(rawValues);
        _sql.Append($"{column} IN {paramSql}");
    }

    /// <summary>
    /// Appends a LIKE predicate that matches the start of the string.
    /// </summary>
    /// <param name="memberExpr">Member representing the column.</param>
    /// <param name="argument">Expression producing the search value.</param>
    private void AppendLikeStartsWith(MemberExpression memberExpr, Expression argument)
    {
        var column = GetColumnNameForMember(memberExpr);
        var raw = EvaluateExpression(argument);
        var escaped = EscapeLikeValue(raw?.ToString() ?? string.Empty);
        var normalized = NormalizeForCase($"{escaped}%");
        var paramSql = AddParameter(normalized);

        var left = _ignoreCase ? $"LOWER({column})" : column;
        var right = paramSql;

        _sql.Append($"{left} LIKE {right} ESCAPE '\\'");
    }

    /// <summary>
    /// Appends a LIKE predicate that matches the end of the string.
    /// </summary>
    /// <param name="memberExpr">Member representing the column.</param>
    /// <param name="argument">Expression producing the search value.</param>
    private void AppendLikeEndsWith(MemberExpression memberExpr, Expression argument)
    {
        var column = GetColumnNameForMember(memberExpr);
        var raw = EvaluateExpression(argument);
        var escaped = EscapeLikeValue(raw?.ToString() ?? string.Empty);
        var normalized = NormalizeForCase($"%{escaped}");
        var paramSql = AddParameter(normalized);

        var left = _ignoreCase ? $"LOWER({column})" : column;
        var right = paramSql;

        _sql.Append($"{left} LIKE {right} ESCAPE '\\'");
    }

    /// <summary>
    /// Escapes LIKE wildcard characters and escape characters in a value.
    /// </summary>
    /// <param name="value">Value to escape.</param>
    /// <returns>Escaped string safe for LIKE expressions.</returns>
    private static string EscapeLikeValue(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }

    /// <summary>
    /// Determines whether an expression represents a null-like value.
    /// </summary>
    /// <param name="expr">Expression to inspect.</param>
    /// <returns>True when the expression is null or empty string (for Oracle).</returns>
    private bool IsNullLike(Expression expr)
    {
        if (expr is ConstantExpression constant)
            return IsNullLike(constant.Value);

        if (expr is MemberExpression { Expression: ConstantExpression closure } member)
        {
            var value = GetValueFromClosure(closure.Value, member.Member);
            return IsNullLike(value);
        }

        return false;
    }

    /// <summary>
    /// Determines whether a value is considered null-like (null or empty string for Oracle).
    /// </summary>
    /// <param name="value">Value to inspect.</param>
    /// <returns>True when null-like.</returns>
    private bool IsNullLike(object? value)
        => value is null || (_treatEmptyStringAsNull && value is string s && s.Length == 0);

    /// <summary>
    /// Normalizes values for case-insensitive comparisons when needed.
    /// </summary>
    /// <param name="value">Value to normalize.</param>
    /// <returns>Normalized value.</returns>
    private object? NormalizeForCase(object? value)
    {
        if (!_ignoreCase)
            return value;

        if (value is string str)
            return str.ToLowerInvariant();

        return value;
    }

    /// <summary>
    /// Adds a parameter to the parameter bag and appends its placeholder to SQL.
    /// </summary>
    /// <param name="value">Parameter value.</param>
    private void AppendParameter(object? value)
    {
        var paramSql = AddParameter(value);
        _sql.Append(paramSql);
    }

    /// <summary>
    /// Adds a parameter and returns the dialect-formatted placeholder.
    /// </summary>
    /// <param name="value">Parameter value.</param>
    /// <returns>Formatted parameter placeholder.</returns>
    private string AddParameter(object? value)
    {
        var paramKey = $"p{_paramIndex++}";
        _parameters[paramKey] = value ?? DBNull.Value;
        return _dialect.FormatParameter(paramKey);
    }

    /// <summary>
    /// Retrieves the value of a captured closure field or property.
    /// </summary>
    /// <param name="closureObject">Closure object instance.</param>
    /// <param name="member">Field or property info.</param>
    /// <returns>Extracted value.</returns>
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

    /// <summary>
    /// Evaluates an expression by compiling and executing it when not a constant.
    /// Uses caching to avoid recompiling the same expressions.
    /// </summary>
    /// <param name="expr">Expression to evaluate.</param>
    /// <returns>Resulting value.</returns>
    private static object? EvaluateExpression(Expression expr)
    {
        if (expr is ConstantExpression constant)
            return constant.Value;

        var lambda = Expression.Lambda(expr);
        var cacheKey = new ExpressionCacheKey(lambda);

        var compiled = _compiledExpressionCache.GetOrAdd(cacheKey, _ =>
        {
            if (_compiledExpressionCache.Count >= MaxCacheSize)
            {
                _compiledExpressionCache.Clear();
            }
            return lambda.Compile();
        });

        return compiled.DynamicInvoke();
    }

    /// <summary>
    /// Cache key for compiled expressions based on expression tree structural hash.
    /// Uses structural hashing for performance and reliability instead of ToString().
    /// </summary>
    /// <remarks>
    /// <para><b>Why not ToString()?</b></para>
    /// <list type="bullet">
    /// <item>ToString() output is not guaranteed to be stable across .NET versions</item>
    /// <item>ToString() allocates strings for every cache lookup</item>
    /// <item>ToString() output varies with parameter names (structurally equivalent expressions differ)</item>
    /// </list>
    /// <para><b>Structural Hashing Benefits:</b></para>
    /// <list type="bullet">
    /// <item>Zero allocations for cache lookups</item>
    /// <item>Structurally equivalent expressions produce same hash</item>
    /// <item>.NET version independent</item>
    /// <item>Better collision resistance through tree-based hashing</item>
    /// </list>
    /// </remarks>
    private readonly struct ExpressionCacheKey : IEquatable<ExpressionCacheKey>
    {
        private readonly int _hashCode;
        private readonly LambdaExpression _expression;

        public ExpressionCacheKey(LambdaExpression lambda)
        {
            _expression = lambda ?? throw new ArgumentNullException(nameof(lambda));
            _hashCode = ExpressionStructuralHasher.ComputeHash(lambda);
        }

        public bool Equals(ExpressionCacheKey other)
        {
            // Fast path: different hash codes = definitely not equal
            if (_hashCode != other._hashCode)
                return false;

            // Hash collision check: use structural equality comparer
            return ExpressionStructuralEqualityComparer.AreEqual(_expression, other._expression);
        }

        public override bool Equals(object? obj)
            => obj is ExpressionCacheKey key && Equals(key);

        public override int GetHashCode() => _hashCode;
    }
}
