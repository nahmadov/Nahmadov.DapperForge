using System.Data;
using System.Data.Common;

namespace DapperToolkit.UnitTests.Fakes;

public class FakeDbTransaction(DbConnection connection) : DbTransaction
{
    private readonly DbConnection _connection = connection;

    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
    protected override DbConnection DbConnection => _connection;

    public override void Commit() { }
    public override void Rollback() { }
}