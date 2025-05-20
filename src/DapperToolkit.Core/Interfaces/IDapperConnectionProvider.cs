using System.Data;

namespace DapperToolkit.Core.Interfaces;

public interface IDapperConnectionProvider
{
    IDbConnection CreateConnection();
}