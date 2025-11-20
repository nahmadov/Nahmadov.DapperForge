using System.Data;
using System.Data.Common;

namespace DapperToolkit.UnitTests.Fakes;
#nullable disable
public class FakeDbCommand(DbConnection conn) : DbCommand
{
    private readonly DbConnection _conn = conn;

    public override string CommandText { get; set; } = string.Empty;

    public override int CommandTimeout { get; set; } = 30;
    public override CommandType CommandType { get; set; } = CommandType.Text;

    protected override DbConnection DbConnection
    {
        get => _conn;
        set { /* ignore */ }
    }

    protected override DbParameterCollection DbParameterCollection { get; } = new FakeDbParameterCollection();
    protected override DbTransaction DbTransaction { get; set; }

    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }

    public override void Cancel() { }

    protected override DbParameter CreateDbParameter() => new FakeDbParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        => throw new NotSupportedException("FakeDbCommand does not support ExecuteReader");

    public override int ExecuteNonQuery() => 0;

    public override object ExecuteScalar() => null;

    public override void Prepare() { }
}
#nullable restore