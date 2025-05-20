using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using DapperToolkit.Core.Extensions;

namespace DapperToolkit.Oracle.Extensions;

public static class DapperOracleServiceCollectionExtensions
{
    public static IServiceCollection AddDapperDbContextWithOracle(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionName = "OracleDb",
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        var connStr = configuration.GetConnectionString(connectionName)
            ?? throw new ArgumentNullException($"Connection string '{connectionName}' not found.");
        return services.AddDapperDbContext(provider => new OracleConnectionProvider(connStr), lifetime);
    }
}