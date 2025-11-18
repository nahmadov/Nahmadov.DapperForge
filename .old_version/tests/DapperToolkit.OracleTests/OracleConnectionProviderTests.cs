using System.Data;

using DapperToolkit.Oracle;

using Oracle.ManagedDataAccess.Client;

namespace DapperToolkit.OracleTests;

public class OracleConnectionProviderTests
{
    [Fact]
    public void Should_Create_OracleConnection_With_Correct_ConnectionString()
    {
        // Arrange
        var connStr = "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=)));User Id=;Password=;";
        var provider = new OracleConnectionProvider(connStr);

        // Act
        var connection = provider.CreateConnection();

        // Assert
        Assert.IsType<OracleConnection>(connection);
        Assert.Equal(connStr, connection.ConnectionString);
        Assert.Equal(ConnectionState.Closed, connection.State);
    }
}