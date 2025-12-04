using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.Oracle;

public class OracleDialect : ISqlDialect
{
    public static readonly OracleDialect Instance = new();

    public string Name => "Oracle";

    public string FormatParameter(string baseName) => ":" + baseName;

    public string QuoteIdentifier(string identifier) => $"\"{identifier}\"";

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

    public string FormatBoolean(bool value) => value ? "1" : "0";
}
