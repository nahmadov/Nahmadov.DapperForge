using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.Oracle;

public class OracleDialect : ISqlDialect
{
    public static readonly OracleDialect Instance = new();

    public string Name => "Oracle";

    public string FormatParameter(string baseName) => ":" + baseName;

    public string QuoteIdentifier(string identifier) => $"\"{identifier}\"";

    public string BuildInsertReturningId(string baseInsertSql, string tableName, string keyColumnName)
    {
        throw new NotSupportedException("Oracle identity returning is not implemented yet.");
    }
}