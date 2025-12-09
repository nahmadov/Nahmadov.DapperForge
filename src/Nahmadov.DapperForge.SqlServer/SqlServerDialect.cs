using Nahmadov.DapperForge.Core.Interfaces;

namespace Nahmadov.DapperForge.SqlServer;

/// <summary>
/// SQL Server-specific dialect implementation.
/// </summary>
public class SqlServerDialect : ISqlDialect
{
    /// <summary>
    /// Singleton instance of the dialect.
    /// </summary>
    public static readonly SqlServerDialect Instance = new();

    /// <summary>
    /// Gets the dialect name.
    /// </summary>
    public string Name => "SqlServer";

    /// <summary>
    /// Gets the default schema for SQL Server.
    /// </summary>
    public string? DefaultSchema => "dbo";

    /// <summary>
    /// Formats a parameter name using SQL Server syntax.
    /// </summary>
    public string FormatParameter(string baseName) => "@" + baseName;

    /// <summary>
    /// Quotes an identifier using SQL Server bracket syntax.
    /// </summary>
    public string QuoteIdentifier(string identifier) => $"[{identifier}]";

    /// <summary>
    /// Builds an INSERT statement that returns the generated identity value.
    /// </summary>
    /// <param name="baseInsertSql">Base INSERT SQL.</param>
    /// <param name="tableName">Fully qualified table name.</param>
    /// <param name="keyColumnNames">Key column names to return.</param>
    /// <returns>INSERT statement with identity retrieval.</returns>
    public string BuildInsertReturningId(string baseInsertSql, string tableName, params string[] keyColumnNames)
    {
        if (keyColumnNames is null || keyColumnNames.Length == 0)
            throw new ArgumentNullException(nameof(keyColumnNames));

        var key = keyColumnNames[0];
        return $"{baseInsertSql}; SELECT CAST(SCOPE_IDENTITY() AS int) AS {QuoteIdentifier(key)};";
    }

    /// <summary>
    /// Formats a boolean literal for SQL Server.
    /// </summary>
    public string FormatBoolean(bool value) => value ? "1" : "0";
}
