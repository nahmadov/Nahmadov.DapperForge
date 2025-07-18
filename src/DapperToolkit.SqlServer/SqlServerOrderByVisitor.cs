using DapperToolkit.Core.Common;

namespace DapperToolkit.SqlServer;

public class SqlServerOrderByVisitor : BaseOrderByVisitor
{
    protected override string FormatColumn(string columnName) => columnName;
}