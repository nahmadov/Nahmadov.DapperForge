using DapperToolkit.Core.Common;

namespace DapperToolkit.SqlServer;

public class SqlServerProjectionVisitor : BaseProjectionVisitor
{
    public SqlServerProjectionVisitor(Type sourceType) : base(sourceType)
    {
    }

    protected override string FormatColumn(string columnName) => columnName;
    protected override string FormatAlias(string alias) => alias;
}