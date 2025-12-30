using System.Data;

using Nahmadov.DapperForge.Core.Interfaces;

namespace Nahmadov.DapperForge.Oracle;

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
    /// Formats a table alias using Oracle syntax (no AS keyword).
    /// </summary>
    public string FormatTableAlias(string alias) => alias;

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

    public bool TryMapDbType(Type clrType, out DbType dbType)
    {
        clrType = Nullable.GetUnderlyingType(clrType) ?? clrType;

        if (clrType.IsEnum)
            clrType = Enum.GetUnderlyingType(clrType);

        if (clrType == typeof(int)) { dbType = DbType.Int32; return true; }
        if (clrType == typeof(long)) { dbType = DbType.Int64; return true; }
        if (clrType == typeof(short)) { dbType = DbType.Int16; return true; }
        if (clrType == typeof(byte)) { dbType = DbType.Byte; return true; }

        if (clrType == typeof(decimal)) { dbType = DbType.Decimal; return true; }
        if (clrType == typeof(double)) { dbType = DbType.Double; return true; }
        if (clrType == typeof(float)) { dbType = DbType.Single; return true; }

        if (clrType == typeof(bool)) { dbType = DbType.Int16; return true; }
        if (clrType == typeof(DateTime)) { dbType = DbType.DateTime; return true; }
        if (clrType == typeof(DateTimeOffset)) { dbType = DbType.DateTimeOffset; return true; }

        if (clrType == typeof(Guid)) { dbType = DbType.Guid; return true; }
        if (clrType == typeof(string)) { dbType = DbType.String; return true; }
        if (clrType == typeof(byte[])) { dbType = DbType.Binary; return true; }

        dbType = default;
        return false;
    }
}
