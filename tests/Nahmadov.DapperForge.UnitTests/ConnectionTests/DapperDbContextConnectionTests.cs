#pragma warning disable CS0618 // Type or member is obsolete - Testing legacy transaction API

using System.Data;

using Nahmadov.DapperForge.Core.Common;
using Nahmadov.DapperForge.UnitTests.Fakes;
using Nahmadov.DapperForge.SqlServer;

namespace Nahmadov.DapperForge.UnitTests.ConnectionTests;

public class DapperDbContextConnectionTests
{
    private static TestDapperDbContext CreateContext(out FakeDbConnection fakeConnection)
    {
        var conn = new FakeDbConnection();
        fakeConnection = conn;
        var options = new DapperDbContextOptions<TestDapperDbContext>
        {
            ConnectionFactory = () => conn,
            Dialect = SqlServerDialect.Instance
        };

        return new TestDapperDbContext(options);
    }

    [Fact]
    public void Connection_Is_Opened_Once_And_Reused()
    {
        var ctx = CreateContext(out var conn);

        var c1 = ctx.ExposeConnection();
        var c2 = ctx.ExposeConnection();

        Assert.Same(conn, c1);
        Assert.Same(conn, c2);
        Assert.Equal(ConnectionState.Open, conn.State);
        Assert.Equal(1, conn.OpenCount);
    }

    [Fact]
    public void Broken_Connection_Is_Disposed_And_Recreated()
    {
        var ctx = CreateContext(out var conn1);
        var c1 = ctx.ExposeConnection();
        Assert.Same(conn1, c1);
        conn1.SetState(ConnectionState.Broken);

        var c2 = ctx.ExposeConnection();

        Assert.Same(conn1, c2);
        Assert.Equal(1, conn1.DisposeCount);
        Assert.Equal(ConnectionState.Open, c2.State);
    }

    [Fact]
    public async Task BeginTransaction_Uses_Same_Connection()
    {
        var ctx = CreateContext(out var conn);

        var transaction = await ctx.BeginTransactionAsync();

        Assert.NotNull(transaction);
        Assert.Same(conn, transaction.Connection);
        Assert.Equal(ConnectionState.Open, conn.State);
    }
}
