#nullable disable
using System.Data;

using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.CoreTests;

public class DapperConnectionProviderTests
{
    private class FakeConnectionProvider : IDapperConnectionProvider
    {
        public IDbConnection CreateConnection() => new FakeConnection();

        private class FakeConnection : IDbConnection
        {
            public string ConnectionString { get; set; } = string.Empty;
            public int ConnectionTimeout => 15;
            public string Database => "FakeDB";
            public ConnectionState State => ConnectionState.Open;
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
        }
    }

    [Fact]
    public void Should_Return_Valid_Connection()
    {
        var provider = new FakeConnectionProvider();
        var connection = provider.CreateConnection();

        Assert.NotNull(connection);
        Assert.Equal(ConnectionState.Open, connection.State);
    }
}