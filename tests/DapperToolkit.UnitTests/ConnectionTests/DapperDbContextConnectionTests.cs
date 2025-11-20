using System.Data;

using DapperToolkit.Core.Common;
using DapperToolkit.UnitTests.Fakes;

namespace DapperToolkit.UnitTests.ConnectionTests;

public class DapperDbContextConnectionTests
{
    private static TestDapperDbContext CreateContext(out FakeDbConnection fakeConnection)
    {
        // 1) Bir dənə connection yarat
        var conn = new FakeDbConnection();

        // 2) out parametrə onu ver
        fakeConnection = conn;

        // 3) Factory həmişə həmin conn-u qaytarsın
        var options = new DapperDbContextOptions<TestDapperDbContext>
        {
            ConnectionFactory = () => conn
        };

        // 4) Context-i də həmin options-lə yarat
        return new TestDapperDbContext(options);
    }

    [Fact]
    public void Connection_Is_Opened_Once_And_Reused()
    {
        // arrange
        var ctx = CreateContext(out var conn);

        // act
        var c1 = ctx.ExposeConnection();
        var c2 = ctx.ExposeConnection();

        // assert
        Assert.Same(conn, c1);
        Assert.Same(conn, c2);
        Assert.Equal(ConnectionState.Open, conn.State);
        Assert.Equal(1, conn.OpenCount); // yalnız bir dəfə Open() çağrılıb
    }

    [Fact]
    public void Broken_Connection_Is_Disposed_And_Recreated()
    {
        // arrange
        var ctx = CreateContext(out var conn1);

        // first usage: opens once
        var c1 = ctx.ExposeConnection();
        Assert.Same(conn1, c1);

        // simulate broken
        conn1.SetState(ConnectionState.Broken);

        // act
        var c2 = ctx.ExposeConnection();

        // assert
        Assert.Same(conn1, c2);                 // yeni connection yaradılıb
        Assert.Equal(1, conn1.DisposeCount);       // köhnə dispose olunub
        Assert.Equal(ConnectionState.Open, c2.State);
    }

    [Fact]
    public async Task BeginTransaction_Uses_Same_Connection()
    {
        // arrange
        var ctx = CreateContext(out var conn);

        // act
        var transaction = await ctx.BeginTransactionAsync();

        // assert
        Assert.NotNull(transaction);
        Assert.Same(conn, transaction.Connection);
        Assert.Equal(ConnectionState.Open, conn.State);
    }
}