using DapperToolkit.Core.Common;

namespace DapperToolkit.Oracle;

public class OracleOrderByVisitor : BaseOrderByVisitor
{
    protected override string FormatColumn(string columnName) => columnName;
}