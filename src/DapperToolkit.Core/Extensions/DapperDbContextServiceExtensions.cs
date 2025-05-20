using Microsoft.Extensions.DependencyInjection;

using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.Core.Extensions;

public static class DapperDbContextServiceExtensions
{
    public static IServiceCollection AddDapperDbContext<TProvider>(
        this IServiceCollection services,
        Func<IServiceProvider, TProvider> providerFactory,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TProvider : class, IDapperConnectionProvider
    {
        services.Add(new ServiceDescriptor(typeof(IDapperConnectionProvider), providerFactory, lifetime));
        services.Add(new ServiceDescriptor(typeof(IDapperDbContext), typeof(Context.DapperDbContext), lifetime));
        return services;
    }
}