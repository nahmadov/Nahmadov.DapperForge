namespace DapperToolkit.Core.Interfaces;

public interface ISqlDialect
{
    string Name { get; }
    string FormatParameter(string baseName);
    string QuoteIdentifier(string identifier);
    string BuildInsertReturningId(string baseInsertSql, string tableName, params string[] keyColumnNames);
    string FormatBoolean(bool value);
}
