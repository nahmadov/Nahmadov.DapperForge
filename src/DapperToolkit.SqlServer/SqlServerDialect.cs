using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.SqlServer;

public class SqlServerDialect : ISqlDialect
{
    public static readonly SqlServerDialect Instance = new();

    public string Name => "SqlServer";

    public string FormatParameter(string baseName) => "@" + baseName;

    public string QuoteIdentifier(string identifier) => $"[{identifier}]";

    public string BuildInsertReturningId(string baseInsertSql, string tableName, string keyColumnName)
    {
        return $"{baseInsertSql}; SELECT CAST(SCOPE_IDENTITY() AS int) AS {QuoteIdentifier(keyColumnName)};";
    }
}