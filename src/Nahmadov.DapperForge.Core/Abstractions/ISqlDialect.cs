using System.Data;

using Nahmadov.DapperForge.Core.Modeling.Mapping;
using Nahmadov.DapperForge.Core.Modeling.Schema;
using Nahmadov.DapperForge.Core.Querying.Predicates;

namespace Nahmadov.DapperForge.Core.Abstractions;
/// <summary>
/// Defines SQL dialect-specific formatting for identifiers, parameters, and generated SQL fragments.
/// </summary>
public interface ISqlDialect
{
    string Name { get; }

    string? DefaultSchema { get; }

    string FormatParameter(string baseName);

    string QuoteIdentifier(string identifier);

    string FormatTableAlias(string alias);

    /// <summary>
    /// Builds SQL that returns key values after an insert based on the dialect's syntax.
    /// </summary>
    string BuildInsertReturningId(string baseInsertSql, string tableName, params string[] keyColumnNames);

    string FormatBoolean(bool value);

    bool TryMapDbType(Type clrType, out DbType dbType);

    // ── Schema / column type resolution ───────────────────────────────────────

    /// <summary>
    /// Resolves a dialect-agnostic <see cref="SqlColumnType"/> and its facets to a concrete DDL
    /// type name (e.g. <c>nvarchar(50)</c>, <c>decimal(18,4)</c>, <c>INTEGER</c>).
    /// </summary>
    /// <remarks>
    /// The default implementation throws <see cref="NotSupportedException"/>. Dialects that support
    /// temp-table / DDL generation (SQL Server, SQLite) override this.
    /// </remarks>
    string GetColumnTypeSql(SqlColumnType type, ColumnTypeFacets facets)
        => throw new NotSupportedException(
            $"Dialect '{Name}' does not support SQL column type resolution.");

    // ── Session temp tables ───────────────────────────────────────────────────

    /// <summary>
    /// Indicates whether the dialect supports runtime-created session temp tables.
    /// Defaults to <see langword="false"/>; SQL Server and SQLite override to <see langword="true"/>.
    /// </summary>
    bool SupportsSessionTempTables => false;

    /// <summary>
    /// Normalises a caller-supplied temp-table name to the dialect's required form
    /// (e.g. SQL Server ensures a leading <c>#</c>; SQLite quotes the bare name).
    /// </summary>
    string FormatTempTableName(string name)
        => throw new NotSupportedException(
            $"Dialect '{Name}' does not support session temp tables.");

    /// <summary>
    /// Wraps a pre-built, comma-separated column DDL fragment in the dialect's
    /// <c>CREATE [TEMP] TABLE</c> statement.
    /// </summary>
    /// <param name="tempName">The formatted temp-table name (from <see cref="FormatTempTableName"/>).</param>
    /// <param name="columnsDdl">Comma-separated column definitions, e.g. <c>"Id" int NOT NULL, ...</c>.</param>
    string BuildCreateTempTable(string tempName, string columnsDdl)
        => throw new NotSupportedException(
            $"Dialect '{Name}' does not support session temp tables.");

    // ── Bulk copy ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Indicates whether the dialect supports bulk copy. Defaults to <see langword="false"/>;
    /// SQL Server (native <c>SqlBulkCopy</c>) and SQLite (batched-insert fallback) override to <see langword="true"/>.
    /// </summary>
    bool SupportsBulkCopy => false;

    /// <summary>
    /// Creates the provider-specific bulk-copy executor.
    /// </summary>
    /// <exception cref="NotSupportedException">The dialect does not support bulk copy.</exception>
    IBulkCopyExecutor CreateBulkCopyExecutor()
        => throw new NotSupportedException($"Dialect '{Name}' does not support bulk copy.");

    // ── Predicate translator factory ──────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="PredicateVisitor{TEntity}"/> for this dialect.
    /// Dialect packages override this to return a dialect-specific subclass
    /// (e.g. <c>SqlitePredicateVisitor&lt;TEntity&gt;</c>) that supports
    /// additional expression patterns such as <c>date()</c> / <c>datetime()</c>.
    /// </summary>
    PredicateVisitor<TEntity> CreatePredicateVisitor<TEntity>(EntityMapping mapping) where TEntity : class
        => new PredicateVisitor<TEntity>(mapping, this);

    /// <summary>
    /// Creates a <see cref="SqlPredicateTranslator"/> with the given alias and parameter prefix.
    /// Used internally for Include-filter translation where the alias differs from the root query.
    /// Dialect packages override this alongside <see cref="CreatePredicateVisitor{TEntity}"/>
    /// to ensure Include filters also benefit from dialect-specific expression support.
    /// </summary>
    SqlPredicateTranslator CreatePredicateTranslator(EntityMapping mapping, string alias = "a", string paramPrefix = "p")
        => new SqlPredicateTranslator(mapping, this, alias, paramPrefix);
}

