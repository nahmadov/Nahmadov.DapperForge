using DapperToolkit.Core.Common;

namespace DapperToolkit.Oracle;

public class OracleProjectionVisitor : BaseProjectionVisitor
{
    public OracleProjectionVisitor(Type sourceType) : base(sourceType)
    {
    }

    protected override string FormatColumn(string columnName) => columnName;
    protected override string FormatAlias(string alias) => alias;
}