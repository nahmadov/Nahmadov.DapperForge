#nullable disable
using System.Data;

using Microsoft.Extensions.DependencyInjection;

using DapperToolkit.Core.Extensions;
using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.CoreTests;

public class DapperDbContextServiceExtensionsTests
{
    private class FakeProvider : IDapperConnectionProvider
    {
        public IDbConnection CreateConnection() => new FakeConnection();
    }

    private class FakeConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 0;
        public string Database => "";
        public static ConnectionState State => ConnectionState.Open;

        ConnectionState IDbConnection.State => throw new NotImplementedException();

        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotImplementedException();
        public void Open() { }
        public void Dispose() { }

        public IDbTransaction BeginTransaction()
        {
            throw new NotImplementedException();
        }

        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            throw new NotImplementedException();
        }

        IDbCommand IDbConnection.CreateCommand()
        {
            throw new NotImplementedException();
        }
    }

    private class FakeContext : IDapperDbContext
    {
        public Task<int> ExecuteAsync(string sql, object param = null) => Task.FromResult(0);
        public Task<IEnumerable<T>> QueryAsync<T>(string sql, object param = null) => Task.FromResult(Enumerable.Empty<T>());
        public Task<T> QueryFirstOrDefaultAsync<T>(string sql, object param = null) => Task.FromResult<T>(default);
        public Task<IDbTransaction> BeginTransactionAsync() => throw new NotImplementedException();
    }

    [Fact]
    public void Should_Register_Dapper_Services()
    {
        var services = new ServiceCollection();
        services.AddDapperDbContext<FakeProvider, FakeContext>(_ => new FakeProvider());

        var provider = services.BuildServiceProvider();

        var resolved = provider.GetService<IDapperDbContext>();
        Assert.IsType<FakeContext>(resolved);
    }
}