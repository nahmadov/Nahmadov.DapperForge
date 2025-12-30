using System.Data;

namespace Nahmadov.DapperForge.Core.Interfaces;

/// <summary>
/// Defines SQL dialect-specific formatting for identifiers, parameters, and generated SQL fragments.
/// </summary>
public interface ISqlDialect
{
    /// <summary>
    /// Gets the unique name of the dialect (e.g., SqlServer, Oracle).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the default schema used by the dialect, if any.
    /// </summary>
    string? DefaultSchema { get; }

    /// <summary>
    /// Formats a parameter name according to the dialect (e.g., "@p0" or ":p0").
    /// </summary>
    /// <param name="baseName">Base parameter name without prefix.</param>
    /// <returns>The formatted parameter name.</returns>
    string FormatParameter(string baseName);

    /// <summary>
    /// Quotes an identifier such as a table or column name for safe usage in SQL.
    /// </summary>
    /// <param name="identifier">Identifier to quote.</param>
    /// <returns>The quoted identifier.</returns>
    string QuoteIdentifier(string identifier);

    /// <summary>
    /// Formats a table alias according to the dialect (e.g., "AS a" vs "a").
    /// </summary>
    /// <param name="alias">Alias to format.</param>
    /// <returns>Dialect-specific table alias fragment.</returns>
    string FormatTableAlias(string alias);

    /// <summary>
    /// Builds SQL that returns key values after an insert based on the dialect's syntax.
    /// </summary>
    /// <param name="baseInsertSql">Insert statement without returning clause.</param>
    /// <param name="tableName">Fully qualified table name.</param>
    /// <param name="keyColumnNames">Key column names to return.</param>
    /// <returns>SQL that performs the insert and returns the key values.</returns>
    string BuildInsertReturningId(string baseInsertSql, string tableName, params string[] keyColumnNames);

    /// <summary>
    /// Formats a boolean literal for use in SQL.
    /// </summary>
    /// <param name="value">The boolean value to format.</param>
    /// <returns>Dialect-specific representation of the boolean value.</returns>
    string FormatBoolean(bool value);

    /// <summary>
    /// Maps CLR type to DbType for Dapper parameters (especially OUTPUT parameters).
    /// </summary>
    bool TryMapDbType(Type clrType, out DbType dbType);
}
