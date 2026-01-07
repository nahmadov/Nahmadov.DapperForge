using System.Data;
using System.Data.Common;

namespace Nahmadov.DapperForge.UnitTests.Fakes;

public class FakeDbTransaction(DbConnection connection) : DbTransaction
{
    private readonly DbConnection _connection = connection;

    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
    protected override DbConnection DbConnection => _connection;

    /// <summary>
    /// Callback invoked when Commit() is called.
    /// </summary>
    public Action? OnCommit { get; set; }

    /// <summary>
    /// Callback invoked when Rollback() is called.
    /// </summary>
    public Action? OnRollback { get; set; }

    public override void Commit()
    {
        OnCommit?.Invoke();
    }

    public override void Rollback()
    {
        OnRollback?.Invoke();
    }
}
