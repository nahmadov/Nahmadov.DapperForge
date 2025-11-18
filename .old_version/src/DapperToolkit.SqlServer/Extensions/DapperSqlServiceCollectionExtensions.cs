using Microsoft.Extensions.DependencyInjection;

using DapperToolkit.Core.Extensions;

namespace DapperToolkit.SqlServer.Extensions;

public static class DapperSqlServiceCollectionExtensions
{
    public static IServiceCollection AddDapperDbContextWithSql<TContext>(
        this IServiceCollection services,
        string connectionString,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        return services.AddDapperDbContext<SqlServerConnectionProvider, TContext>(provider => new SqlServerConnectionProvider(connectionString), lifetime);
    }
}