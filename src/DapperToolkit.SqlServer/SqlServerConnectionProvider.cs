using System.Data;

using Microsoft.Data.SqlClient;

using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.SqlServer;

public class SqlServerConnectionProvider(string connectionString) : IDapperConnectionProvider
{
    private readonly string _connectionString = connectionString;

    public IDbConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }
}