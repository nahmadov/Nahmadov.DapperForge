using System.Data;

using DapperToolkit.Core.Interfaces;

using Oracle.ManagedDataAccess.Client;

namespace DapperToolkit.Oracle;

public sealed class OracleConnectionProvider<TContext>(string connectionString) : IDapperConnectionProvider<TContext>
    where TContext : IDapperDbContext
{
    public IDbConnection CreateConnection()
        => new OracleConnection(connectionString);
}