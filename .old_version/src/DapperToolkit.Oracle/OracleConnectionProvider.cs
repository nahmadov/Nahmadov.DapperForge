using System.Data;

using DapperToolkit.Core.Interfaces;

using Oracle.ManagedDataAccess.Client;

namespace DapperToolkit.Oracle;

public class OracleConnectionProvider(string connectionString) : IDapperConnectionProvider
{
    private readonly string _connectionString = connectionString;

    public IDbConnection CreateConnection()
    {
        return new OracleConnection(_connectionString);
    }
}