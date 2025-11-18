using System.Data;

namespace DapperToolkit.Core.Interfaces;

public interface IDapperConnectionProvider<TContext> where TContext : IDapperDbContext
{
    IDbConnection CreateConnection();
}