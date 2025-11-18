using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using DapperToolkit.Core.Extensions;

namespace DapperToolkit.Oracle.Extensions;

public static class DapperOracleServiceCollectionExtensions
{
    public static IServiceCollection AddDapperDbContextWithSql<TContext>(
        this IServiceCollection services,
        string connectionString,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        return services.AddDapperDbContext<OracleConnectionProvider, TContext>(provider => new OracleConnectionProvider(connectionString), lifetime);
    }
}