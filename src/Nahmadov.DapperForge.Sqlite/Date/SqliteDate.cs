namespace Nahmadov.DapperForge.Sqlite.Date;

/// <summary>
/// Provides expression-tree marker methods for SQLite's built-in date functions.
/// Use these methods inside LINQ predicates passed to <c>WhereAsync</c>, <c>Where</c>,
/// <c>FirstOrDefaultAsync</c>, etc. — they are never executed at runtime.
/// </summary>
/// <remarks>
/// <para><b>How it works:</b></para>
/// <para>
/// <see cref="SqlitePredicateTranslator"/> detects calls to these methods in the expression
/// tree and emits the corresponding SQLite SQL function instead of evaluating them:
/// <list type="bullet">
///   <item><see cref="Date(System.DateTime)"/> → <c>date(col)</c> — strips the time component,
///     returns <c>"YYYY-MM-DD"</c>.</item>
///   <item><see cref="DateTime(System.DateTime)"/> → <c>datetime(col)</c> — normalises to
///     <c>"YYYY-MM-DD HH:MM:SS"</c>.</item>
/// </list>
/// </para>
/// <para><b>Usage:</b></para>
/// <code>
/// // Filter by date part only (ignores time stored in the column):
/// await db.Orders.WhereAsync(o => SqliteDate.Date(o.CreatedAt) == DateTime.Today);
///
/// // Range filter on full datetime:
/// await db.Events.WhereAsync(e =>
///     SqliteDate.DateTime(e.StartsAt) >= start &amp;&amp;
///     SqliteDate.DateTime(e.StartsAt) &lt;  end);
///
/// // Nullable columns are fully supported:
/// await db.Orders.WhereAsync(o =>
///     o.ShippedAt.HasValue &amp;&amp;
///     SqliteDate.Date(o.ShippedAt!.Value) == DateTime.Today);
/// </code>
/// <para><b>Important:</b></para>
/// <para>
/// These methods throw <see cref="InvalidOperationException"/> if called directly (outside an
/// expression tree). They are purely compile-time markers for the predicate translator.
/// </para>
/// </remarks>
public static class SqliteDate
{
    /// <summary>
    /// Expression-tree marker for SQLite's <c>date(col)</c> function.
    /// Strips the time component and enables date-only comparisons.
    /// </summary>
    /// <param name="value">An entity DateTime property (used only as an expression marker).</param>
    /// <returns>Never returns — throws if called outside an expression tree.</returns>
    /// <exception cref="InvalidOperationException">
    /// Always thrown when invoked directly. Use only inside LINQ predicate expressions.
    /// </exception>
    public static DateTime Date(DateTime value)
        => throw new InvalidOperationException(
            $"{nameof(SqliteDate)}.{nameof(Date)} is an expression-tree marker only. " +
            "It must be used inside a LINQ predicate expression (e.g. WhereAsync / Where), not called directly.");

    /// <summary>
    /// Expression-tree marker for SQLite's <c>date(col)</c> function on a nullable column.
    /// </summary>
    /// <param name="value">An entity DateTime? property (used only as an expression marker).</param>
    /// <exception cref="InvalidOperationException">Always thrown when invoked directly.</exception>
    public static DateTime Date(DateTime? value)
        => throw new InvalidOperationException(
            $"{nameof(SqliteDate)}.{nameof(Date)} is an expression-tree marker only. " +
            "It must be used inside a LINQ predicate expression (e.g. WhereAsync / Where), not called directly.");

    /// <summary>
    /// Expression-tree marker for SQLite's <c>datetime(col)</c> function.
    /// Normalises the stored value to <c>"YYYY-MM-DD HH:MM:SS"</c> for consistent comparisons.
    /// </summary>
    /// <param name="value">An entity DateTime property (used only as an expression marker).</param>
    /// <exception cref="InvalidOperationException">Always thrown when invoked directly.</exception>
    public static DateTime DateTime(DateTime value)
        => throw new InvalidOperationException(
            $"{nameof(SqliteDate)}.{nameof(DateTime)} is an expression-tree marker only. " +
            "It must be used inside a LINQ predicate expression (e.g. WhereAsync / Where), not called directly.");

    /// <summary>
    /// Expression-tree marker for SQLite's <c>datetime(col)</c> function on a nullable column.
    /// </summary>
    /// <param name="value">An entity DateTime? property (used only as an expression marker).</param>
    /// <exception cref="InvalidOperationException">Always thrown when invoked directly.</exception>
    public static DateTime DateTime(DateTime? value)
        => throw new InvalidOperationException(
            $"{nameof(SqliteDate)}.{nameof(DateTime)} is an expression-tree marker only. " +
            "It must be used inside a LINQ predicate expression (e.g. WhereAsync / Where), not called directly.");

    // ── String column overloads ───────────────────────────────────────────────

    /// <summary>
    /// Expression-tree marker for SQLite's <c>date(col)</c> function on a <see langword="string"/> column.
    /// Use this when the entity stores dates as ISO-8601 strings (e.g. <c>"YYYY-MM-DD"</c> or
    /// <c>"YYYY-MM-DD HH:MM:SS"</c>) and you need date-part extraction or normalisation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the column already stores pure date strings in <c>"YYYY-MM-DD"</c> format, you can
    /// compare them directly without this marker — ISO-8601 date strings sort correctly as
    /// plain strings:
    /// <code>
    /// // This already works without SqliteDate:
    /// await db.Orders.WhereAsync(o => o.Date >= "2024-01-01" &amp;&amp; o.Date &lt;= "2024-12-31");
    /// </code>
    /// Use <c>SqliteDate.Date</c> when the stored string contains a time part and you want to
    /// filter on the date portion only:
    /// <code>
    /// // Column contains "2024-01-15 10:30:00", filter on date part:
    /// await db.Orders.WhereAsync(o => SqliteDate.Date(o.CreatedDateStr) == "2024-01-15");
    /// </code>
    /// </para>
    /// </remarks>
    /// <param name="value">An entity <see langword="string"/> property (expression marker only).</param>
    /// <returns>Never returns — throws if called outside an expression tree.</returns>
    /// <exception cref="InvalidOperationException">Always thrown when invoked directly.</exception>
    public static string Date(string value)
        => throw new InvalidOperationException(
            $"{nameof(SqliteDate)}.{nameof(Date)} is an expression-tree marker only. " +
            "It must be used inside a LINQ predicate expression (e.g. WhereAsync / Where), not called directly.");

    /// <summary>
    /// Expression-tree marker for SQLite's <c>datetime(col)</c> function on a <see langword="string"/> column.
    /// Use this when the entity stores datetime values as strings and you need normalised datetime comparisons.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Typical use case — combine a separate date string column and a time string column for a
    /// full datetime comparison via raw SQL. For a single string column that already stores
    /// <c>"YYYY-MM-DD HH:MM:SS"</c>, <c>datetime(col)</c> normalises the value for comparison:
    /// <code>
    /// await db.Events.WhereAsync(e =>
    ///     SqliteDate.DateTime(e.StartsAtStr) &gt; "2024-01-15 09:00:00");
    /// </code>
    /// </para>
    /// </remarks>
    /// <param name="value">An entity <see langword="string"/> property (expression marker only).</param>
    /// <exception cref="InvalidOperationException">Always thrown when invoked directly.</exception>
    public static string DateTime(string value)
        => throw new InvalidOperationException(
            $"{nameof(SqliteDate)}.{nameof(DateTime)} is an expression-tree marker only. " +
            "It must be used inside a LINQ predicate expression (e.g. WhereAsync / Where), not called directly.");
}
