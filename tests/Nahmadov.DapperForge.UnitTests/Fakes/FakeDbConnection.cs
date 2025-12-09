using System.Data;
using System.Data.Common;

namespace Nahmadov.DapperForge.UnitTests.Fakes;
#nullable disable
public class FakeDbConnection : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;

    public int OpenCount { get; private set; }
    public int DisposeCount { get; private set; }

    public void SetState(ConnectionState state) => _state = state;

    public override string ConnectionString { get; set; } = string.Empty;
    public override string Database => "FakeDb";
    public override string DataSource => "FakeSource";
    public override string ServerVersion => "1.0";
    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName)
    {
        // testlãÙr +-+ç+-n he+ç nãÙ etmirik
    }

    public override void Close()
    {
        _state = ConnectionState.Closed;
    }

    public override void Open()
    {
        _state = ConnectionState.Open;
        OpenCount++;
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        => new FakeDbTransaction(this);

    protected override DbCommand CreateDbCommand()
        => new FakeDbCommand(this);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        DisposeCount++;
        _state = ConnectionState.Closed;
    }
}
#nullable restore
