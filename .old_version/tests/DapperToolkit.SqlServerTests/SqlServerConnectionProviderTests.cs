using System.Data;

using Microsoft.Data.SqlClient;

using DapperToolkit.SqlServer;

namespace DapperToolkit.SqlServerTests;

public class SqlServerConnectionProviderTests
{
    [Fact]
    public void Should_Create_SqlConnection_With_Correct_ConnectionString()
    {
        // Arrange
        var connStr = "Server=.;Database=TestDb;Trusted_Connection=True;";
        var provider = new SqlServerConnectionProvider(connStr);

        // Act
        var connection = provider.CreateConnection();

        // Assert
        Assert.IsType<SqlConnection>(connection);
        Assert.Equal(connStr, connection.ConnectionString);
        Assert.Equal(ConnectionState.Closed, connection.State);
    }
}