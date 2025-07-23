using System.Data;

using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.CoreTests;

public class DapperDbContextInterfaceTests
{
    private class FakeDbContext : IDapperDbContext
    {
        public IDbConnection Connection => throw new NotImplementedException();

        public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
            => Task.FromResult<IEnumerable<T>>([]);

        public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null)
            => Task.FromResult<T?>(default);

        public Task<int> ExecuteAsync(string sql, object? param = null)
            => Task.FromResult(1);

        public Task<IDbTransaction> BeginTransactionAsync()
            => throw new NotImplementedException();
    }

    [Fact]
    public async Task Should_Call_All_Methods()
    {
        var db = new FakeDbContext();

        var result1 = await db.QueryAsync<string>("SELECT * FROM DUAL");
        Assert.Empty(result1);

        var result2 = await db.QueryFirstOrDefaultAsync<string>("SELECT 1");
        Assert.Null(result2);

        var rows = await db.ExecuteAsync("UPDATE TEST SET VAL=1");
        Assert.Equal(1, rows);
    }
}