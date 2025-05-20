using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using DapperToolkit.Core.Extensions;

namespace DapperToolkit.SqlServer.Extensions;

public static class DapperSqlServiceCollectionExtensions
{
    public static IServiceCollection AddDapperDbContextWithSql(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionName = "DefaultConnection",
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        var connStr = configuration.GetConnectionString(connectionName)
            ?? throw new ArgumentNullException($"Connection string '{connectionName}' not found.");
        return services.AddDapperDbContext(provider => new SqlServerConnectionProvider(connStr), lifetime);
    }
}