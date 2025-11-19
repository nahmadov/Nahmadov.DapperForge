using System.Data;

using Microsoft.Data.SqlClient;

using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.SqlServer;

public sealed class SqlServerConnectionProvider<TContext>(string connectionString) : IDapperConnectionProvider<TContext>
    where TContext : IDapperDbContext
{
    public IDbConnection CreateConnection()
        => new SqlConnection(connectionString);
}
