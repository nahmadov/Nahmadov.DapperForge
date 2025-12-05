using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.SqlServer;

public class SqlServerDialect : ISqlDialect
{
    public static readonly SqlServerDialect Instance = new();

    public string Name => "SqlServer";
    public string? DefaultSchema => "dbo";

    public string FormatParameter(string baseName) => "@" + baseName;

    public string QuoteIdentifier(string identifier) => $"[{identifier}]";

    public string BuildInsertReturningId(string baseInsertSql, string tableName, params string[] keyColumnNames)
    {
        if (keyColumnNames is null || keyColumnNames.Length == 0)
            throw new ArgumentNullException(nameof(keyColumnNames));

        var key = keyColumnNames[0];
        return $"{baseInsertSql}; SELECT CAST(SCOPE_IDENTITY() AS int) AS {QuoteIdentifier(key)};";
    }

    public string FormatBoolean(bool value) => value ? "1" : "0";
}
