using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;
using Nahmadov.DapperForge.Core.Querying.Predicates;

namespace Nahmadov.DapperForge.Sqlite.Date;

/// <summary>
/// SQLite-specific predicate translator that extends <see cref="SqlPredicateTranslator"/>
/// with support for <see cref="SqliteDate.Date"/> and <see cref="SqliteDate.DateTime"/>
/// expression markers.
/// </summary>
/// <remarks>
/// <para><b>Supported patterns (in addition to the base translator):</b></para>
/// <list type="bullet">
///   <item>
///     <c>SqliteDate.Date(e.Prop) == value</c> →
///     <c>date(a."Prop") = date(@p0)</c>
///     <br/>Both sides are wrapped in <c>date()</c> to normalise the comparison to
///     <c>"YYYY-MM-DD"</c> strings, regardless of whether <c>value</c> is a
///     <see cref="DateTime"/>, <see cref="DateOnly"/>, or already a date string.
///   </item>
///   <item>
///     <c>SqliteDate.DateTime(e.Prop) &gt; threshold</c> →
///     <c>datetime(a."Prop") &gt; @p0</c>
///     <br/>Only the column side is wrapped; the parameter keeps its full
///     <c>"YYYY-MM-DD HH:MM:SS"</c> representation which matches <c>datetime()</c> output.
///   </item>
/// </list>
/// <para><b>Null semantics:</b></para>
/// <para>
/// SQLite's <c>date(NULL)</c> and <c>datetime(NULL)</c> return <c>NULL</c>, so rows
/// with null date columns are automatically excluded from all comparisons.
/// </para>
/// </remarks>
internal sealed class SqlitePredicateTranslator : SqlPredicateTranslator
{
    public SqlitePredicateTranslator(
        EntityMapping mapping,
        ISqlDialect dialect,
        string alias = "a",
        string paramPrefix = "p")
        : base(mapping, dialect, alias, paramPrefix) { }

    // ── Overrides ─────────────────────────────────────────────────────────────

    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (TryHandleSqliteDateComparison(node))
            return node;

        return base.VisitBinary(node);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Standalone use: SqliteDate.Date(e.Prop) or SqliteDate.DateTime(e.Prop)
        // without a containing binary comparison (e.g. used in an AND chain where
        // one side is already a boolean). Rare but valid.
        if (node.Method.DeclaringType == typeof(SqliteDate)
            && node.Arguments.Count == 1
            && node.Arguments[0] is MemberExpression memberExpr
            && IsEntityProperty(memberExpr))
        {
            var column = GetColumnNameForMember(memberExpr);
            var fn = node.Method.Name == nameof(SqliteDate.Date) ? "date" : "datetime";
            AppendSql($"{fn}({column})");
            return node;
        }

        return base.VisitMethodCall(node);
    }

    // ── Date comparison handler ───────────────────────────────────────────────

    /// <summary>
    /// Detects patterns like <c>SqliteDate.Date(e.Prop) OP value</c> and emits the
    /// appropriate SQLite SQL, wrapping parameters correctly for each function.
    /// </summary>
    private bool TryHandleSqliteDateComparison(BinaryExpression node)
    {
        if (!IsComparisonOperator(node.NodeType))
            return false;

        // Detect which side (if any) is a SqliteDate marker call on an entity property.
        if (!TryExtractDateCall(node.Left, out var fn, out var colMember))
        {
            // Try the other side (e.g. value == SqliteDate.Date(e.Prop))
            if (!TryExtractDateCall(node.Right, out fn, out colMember))
                return false;

            // Swap: re-enter with operands flipped so column is always on the left.
            var flipped = Expression.MakeBinary(FlipOperator(node.NodeType), node.Right, node.Left);
            return TryHandleSqliteDateComparison(flipped);
        }

        var column = GetColumnNameForMember(colMember!);
        var sqlFn = fn == nameof(SqliteDate.Date) ? "date" : "datetime";
        var op = GetComparisonOperator(node.NodeType);

        // Evaluate the right-hand side (constant, closure field, or property).
        var rightValue = EvaluateExpression(node.Right);

        AppendSql("(");
        AppendSql($"{sqlFn}({column})");
        AppendSql(op);

        if (fn == nameof(SqliteDate.Date))
        {
            // Wrap the parameter in date() so the comparison is always "YYYY-MM-DD" = "YYYY-MM-DD".
            // SQLite's date() handles DateTime strings, DateOnly strings, and date-only strings.
            var paramPlaceholder = AddParameter(FormatDateValue(rightValue));
            AppendSql($"date({paramPlaceholder})");
        }
        else
        {
            // For datetime(), Dapper already serialises DateTime as "YYYY-MM-DD HH:MM:SS"
            // which matches datetime() output, so no wrapping is needed.
            AppendSql(AddParameter(rightValue));
        }

        AppendSql(")");
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool TryExtractDateCall(
        Expression expr,
        out string? functionName,
        out MemberExpression? columnMember)
    {
        functionName = null;
        columnMember = null;

        if (expr is not MethodCallExpression call) return false;
        if (call.Method.DeclaringType != typeof(SqliteDate)) return false;
        if (call.Arguments.Count != 1) return false;
        if (call.Arguments[0] is not MemberExpression member) return false;

        functionName = call.Method.Name;
        columnMember = member;
        return true;
    }

    private static bool IsComparisonOperator(ExpressionType t) => t is
        ExpressionType.Equal or ExpressionType.NotEqual or
        ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual or
        ExpressionType.LessThan or ExpressionType.LessThanOrEqual;

    private static string GetComparisonOperator(ExpressionType t) => t switch
    {
        ExpressionType.Equal => " = ",
        ExpressionType.NotEqual => " <> ",
        ExpressionType.GreaterThan => " > ",
        ExpressionType.GreaterThanOrEqual => " >= ",
        ExpressionType.LessThan => " < ",
        ExpressionType.LessThanOrEqual => " <= ",
        _ => throw new NotSupportedException($"Unsupported operator: {t}")
    };

    private static ExpressionType FlipOperator(ExpressionType t) => t switch
    {
        ExpressionType.GreaterThan => ExpressionType.LessThan,
        ExpressionType.GreaterThanOrEqual => ExpressionType.LessThanOrEqual,
        ExpressionType.LessThan => ExpressionType.GreaterThan,
        ExpressionType.LessThanOrEqual => ExpressionType.GreaterThanOrEqual,
        _ => t  // Equal / NotEqual are symmetric
    };

    /// <summary>
    /// Formats a value for use as a <c>date()</c> parameter.
    /// Converts <see cref="DateTime"/> and <see cref="DateOnly"/> to <c>"YYYY-MM-DD"</c>
    /// so SQLite's <c>date()</c> always receives a recognised format.
    /// Other values (strings, nulls) are passed through unchanged.
    /// </summary>
    private static object? FormatDateValue(object? value) => value switch
    {
        DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => value
    };

    /// <summary>
    /// Evaluates a non-entity-parameter expression to its runtime value.
    /// Handles constants, closure fields/properties, and simple Convert nodes.
    /// </summary>
    private static object? EvaluateExpression(Expression expr)
    {
        if (expr is ConstantExpression constant)
            return constant.Value;

        if (expr is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            return EvaluateExpression(unary.Operand);

        if (expr is MemberExpression member)
            return EvaluateMember(member);

        // Fallback for more complex captured expressions.
        var lambda = Expression.Lambda<Func<object?>>(Expression.Convert(expr, typeof(object)));
        return lambda.Compile(preferInterpretation: true)();
    }

    private static object? EvaluateMember(MemberExpression member)
    {
        var target = member.Expression is null ? null : EvaluateExpression(member.Expression);
        return member.Member switch
        {
            FieldInfo fi => fi.GetValue(target),
            PropertyInfo pi => pi.GetValue(target),
            _ => throw new NotSupportedException($"Cannot evaluate member: {member.Member}")
        };
    }
}
