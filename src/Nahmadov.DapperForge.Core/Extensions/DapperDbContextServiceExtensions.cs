using Microsoft.Extensions.DependencyInjection;

using Nahmadov.DapperForge.Core.Common;
using Nahmadov.DapperForge.Core.Context;

namespace Nahmadov.DapperForge.Core.Extensions;

/// <summary>
/// Service registration helpers for integrating <see cref="DapperDbContext"/> with DI.
/// </summary>
public static class DapperDbContextServiceExtensions
{
    /// <summary>
    /// Registers a Dapper context with the DI container and configures its options.
    /// </summary>
    public static IServiceCollection AddDapperDbContext<TContext>(
        this IServiceCollection services,
        Action<DapperDbContextOptionsBuilder<TContext>> configure,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DapperDbContext
    {
        var options = new DapperDbContextOptions<TContext>();
        var builder = new DapperDbContextOptionsBuilder<TContext>(options);

        configure(builder);

        if (options.ConnectionFactory is null)
            throw new InvalidOperationException(
                $"No connection configured for {typeof(TContext).Name}. Call UseSqlServer/UseOracle/etc.");

        services.AddSingleton(options);

        services.Add(
            new ServiceDescriptor(typeof(TContext), typeof(TContext), lifetime));

        return services;
    }
}
