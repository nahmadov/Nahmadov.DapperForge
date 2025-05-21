using Microsoft.Extensions.DependencyInjection;

using DapperToolkit.Core.Extensions;

namespace DapperToolkit.SqlServer.Extensions;

public static class DapperSqlServiceCollectionExtensions
{
    public static IServiceCollection AddDapperDbContextWithSql(
        this IServiceCollection services,
        string connectionString,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        return services.AddDapperDbContext(provider => new SqlServerConnectionProvider(connectionString), lifetime);
    }
}