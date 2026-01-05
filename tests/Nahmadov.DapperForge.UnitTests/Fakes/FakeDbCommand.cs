using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Nahmadov.DapperForge.UnitTests.Fakes;
#nullable disable
public class FakeDbCommand(DbConnection conn) : DbCommand
{
    private readonly FakeDbConnection _fakeConn = (FakeDbConnection)conn;
    public override string CommandText { get; set; } = string.Empty;

    public override int CommandTimeout { get; set; } = 30;
    public override CommandType CommandType { get; set; } = CommandType.Text;

    protected override DbConnection DbConnection
    {
        get => _fakeConn;
        set { }
    }

    protected override DbParameterCollection DbParameterCollection { get; } = new FakeDbParameterCollection();
    protected override DbTransaction DbTransaction { get; set; }

    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }

    public override void Cancel() { }

    protected override DbParameter CreateDbParameter() => new FakeDbParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        var data = _fakeConn.DequeueQuery();
        return new FakeDbDataReader(data);
    }

    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        => Task.FromResult(ExecuteDbDataReader(behavior));

    public override int ExecuteNonQuery() => _fakeConn.DequeueNonQuery();

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => Task.FromResult(ExecuteNonQuery());

    public override object ExecuteScalar() => _fakeConn.DequeueScalar();

    public override Task<object> ExecuteScalarAsync(CancellationToken cancellationToken) => Task.FromResult(ExecuteScalar());

    public override void Prepare() { }
}
#nullable restore
