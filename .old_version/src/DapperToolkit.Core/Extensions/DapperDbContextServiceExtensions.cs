using Microsoft.Extensions.DependencyInjection;

using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.Core.Extensions;

public static class DapperDbContextServiceExtensions
{
    public static IServiceCollection AddDapperDbContext<TProvider, TContext>(
        this IServiceCollection services,
        Func<IServiceProvider, TProvider> providerFactory,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TProvider : class, IDapperConnectionProvider
    {
        services.Add(new ServiceDescriptor(typeof(IDapperConnectionProvider), providerFactory, lifetime));
        services.Add(new ServiceDescriptor(typeof(IDapperDbContext), typeof(TContext), lifetime));
        services.Add(new ServiceDescriptor(typeof(TContext), typeof(TContext), lifetime)); // ⬅️ EF kimi
        return services;
    }
}