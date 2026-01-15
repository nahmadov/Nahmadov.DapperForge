#pragma warning disable CS0618 // Type or member is obsolete - Testing legacy transaction API

using System.Data;
using System.Data.Common;

using Nahmadov.DapperForge.Core.Common;
using Nahmadov.DapperForge.Core.Exceptions;
using Nahmadov.DapperForge.UnitTests.Fakes;
using Nahmadov.DapperForge.SqlServer;
using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Context;

/// <summary>
/// Tests for DapperDbContext async query and execute operations.
/// </summary>
public class DapperDbContextAsyncTests
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
    public async Task QueryAsync_WithValidSql_OpenConnection()
    {
        var ctx = CreateContext(out var conn);

        try
        {
            // Dapper would normally execute; here we test the connection opens
            var result = await ctx.QueryAsync<object>("SELECT 1");
        }
        catch
        {
            // Expected to fail since we're using a fake connection
        }

        Assert.True(conn.OpenCount > 0);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidSql_OpenConnection()
    {
        var ctx = CreateContext(out var conn);

        try
        {
            await ctx.ExecuteAsync("DELETE FROM [Users]");
        }
        catch
        {
            // Expected to fail since we're using a fake connection
        }

        Assert.True(conn.OpenCount > 0);
    }

    [Fact]
    public async Task BeginTransactionAsync_ReturnsValidTransaction()
    {
        var ctx = CreateContext(out var conn);

        var transaction = await ctx.BeginTransactionAsync();

        Assert.NotNull(transaction);
        Assert.IsAssignableFrom<IDbTransaction>(transaction);
        Assert.Equal(ConnectionState.Open, conn.State);
    }

    [Fact]
    public void Constructor_ThrowsWhenNoOptions()
    {
        // DapperDbContext is abstract, so we use TestDapperDbContext
        var options = new DapperDbContextOptions<TestDapperDbContext>
        {
            ConnectionFactory = () => new FakeDbConnection(),
            Dialect = null // Missing dialect
        };

        var exception = Assert.Throws<DapperConfigurationException>(() =>
        {
            new TestDapperDbContext(options);
        });

        Assert.Contains("Dialect is not configured", exception.Message);
    }

    [Fact]
    public void Constructor_ThrowsWhenNoConnectionFactory()
    {
        var options = new DapperDbContextOptions<TestDapperDbContext>
        {
            ConnectionFactory = null, // Missing factory
            Dialect = SqlServerDialect.Instance
        };

        var exception = Assert.Throws<DapperConfigurationException>(() =>
        {
            new TestDapperDbContext(options);
        });

        Assert.Contains("ConnectionFactory is not configured", exception.Message);
    }

    [Fact]
    public void Constructor_ThrowsWhenNullOptions()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            new TestDapperDbContext(null!);
        });
    }

    [Fact]
    public void Dispose_ClosesConnection()
    {
        var ctx = CreateContext(out var conn);
        var connection = ctx.ExposeConnection();

        ctx.Dispose();

        Assert.Equal(ConnectionState.Closed, conn.State);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var ctx = CreateContext(out var conn);
        _ = ctx.ExposeConnection();

        ctx.Dispose();
        ctx.Dispose(); // Should not throw

        Assert.Equal(ConnectionState.Closed, conn.State);
    }
}
