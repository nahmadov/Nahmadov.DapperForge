using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.Oracle;

/// <summary>
/// Oracle-specific SQL dialect implementation.
/// </summary>
public class OracleDialect : ISqlDialect
{
    /// <summary>
    /// Singleton instance of the dialect.
    /// </summary>
    public static readonly OracleDialect Instance = new();

    /// <summary>
    /// Gets the dialect name.
    /// </summary>
    public string Name => "Oracle";

    /// <summary>
    /// Gets the default schema for Oracle (none by default).
    /// </summary>
    public string? DefaultSchema => null;

    /// <summary>
    /// Formats a parameter name using Oracle syntax.
    /// </summary>
    public string FormatParameter(string baseName) => ":" + baseName;

    /// <summary>
    /// Quotes an identifier using Oracle double-quote syntax.
    /// </summary>
    public string QuoteIdentifier(string identifier) => $"\"{identifier}\"";

    /// <summary>
    /// Builds an INSERT statement that returns generated key values using RETURNING INTO.
    /// </summary>
    /// <param name="baseInsertSql">Base INSERT SQL.</param>
    /// <param name="tableName">Fully qualified table name.</param>
    /// <param name="keyColumnNames">Key columns to return.</param>
    /// <returns>INSERT statement with RETURNING clause.</returns>
    public string BuildInsertReturningId(string baseInsertSql, string tableName, params string[] keyColumnNames)
    {
        if (keyColumnNames is null || keyColumnNames.Length == 0)
            throw new ArgumentNullException(nameof(keyColumnNames));

        if (keyColumnNames.Length == 1)
        {
            var key = keyColumnNames[0];
            return $"{baseInsertSql} RETURNING {QuoteIdentifier(key)} INTO {FormatParameter(key)}";
        }

        var returning = string.Join(", ", keyColumnNames.Select(QuoteIdentifier));
        var into = string.Join(", ", keyColumnNames.Select(FormatParameter));
        return $"{baseInsertSql} RETURNING {returning} INTO {into}";
    }

    /// <summary>
    /// Formats a boolean literal for Oracle SQL.
    /// </summary>
    public string FormatBoolean(bool value) => value ? "1" : "0";
}
