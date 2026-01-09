using System.Data;

namespace Nahmadov.DapperForge.Core.Interfaces;

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
}
