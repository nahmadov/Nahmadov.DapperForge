using System.Data;

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
    /// Formats a table alias using SQL Server syntax.
    /// </summary>
    public string FormatTableAlias(string alias) => $"AS {alias}";

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

    public bool TryMapDbType(Type clrType, out DbType dbType)
    {
        clrType = Nullable.GetUnderlyingType(clrType) ?? clrType;
        if (clrType.IsEnum) clrType = Enum.GetUnderlyingType(clrType);

        if (clrType == typeof(int)) { dbType = DbType.Int32; return true; }
        if (clrType == typeof(long)) { dbType = DbType.Int64; return true; }
        if (clrType == typeof(short)) { dbType = DbType.Int16; return true; }
        if (clrType == typeof(byte)) { dbType = DbType.Byte; return true; }

        if (clrType == typeof(decimal)) { dbType = DbType.Decimal; return true; }
        if (clrType == typeof(double)) { dbType = DbType.Double; return true; }
        if (clrType == typeof(float)) { dbType = DbType.Single; return true; }

        if (clrType == typeof(bool)) { dbType = DbType.Boolean; return true; }
        if (clrType == typeof(DateTime)) { dbType = DbType.DateTime; return true; }
        if (clrType == typeof(DateTimeOffset)) { dbType = DbType.DateTimeOffset; return true; }

        if (clrType == typeof(Guid)) { dbType = DbType.Guid; return true; }
        if (clrType == typeof(string)) { dbType = DbType.String; return true; }
        if (clrType == typeof(byte[])) { dbType = DbType.Binary; return true; }

        dbType = default;
        return false;
    }
}
