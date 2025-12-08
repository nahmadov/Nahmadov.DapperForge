using Microsoft.Extensions.DependencyInjection;

using DapperToolkit.Core.Common;
using DapperToolkit.Core.Context;
using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.Core.Extensions;

/// <summary>
/// Service registration helpers for integrating <see cref="DapperDbContext"/> with DI.
/// </summary>
public static class DapperDbContextServiceExtensions
{
    /// <summary>
    /// Registers a Dapper context with the DI container and configures its options.
    /// </summary>
    /// <typeparam name="TContext">Context type to register.</typeparam>
    /// <param name="services">Service collection to register with.</param>
    /// <param name="configure">Callback to configure context options.</param>
    /// <param name="lifetime">Service lifetime for the context.</param>
    /// <returns>The provided service collection.</returns>
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
