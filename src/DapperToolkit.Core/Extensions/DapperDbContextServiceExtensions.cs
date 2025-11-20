using Microsoft.Extensions.DependencyInjection;

using DapperToolkit.Core.Common;
using DapperToolkit.Core.Context;
using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.Core.Extensions;

public static class DapperDbContextServiceExtensions
{
    public static IServiceCollection AddDapperDbContext<TContext>(
        this IServiceCollection services,
        Action<DapperDbContextOptionsBuilder<TContext>> configure,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DapperDbContext
    {
        // options obyektini yaradıb builder-ə veririk
        var options = new DapperDbContextOptions<TContext>();
        var builder = new DapperDbContextOptionsBuilder<TContext>(options);

        configure(builder);

        if (options.ConnectionFactory is null)
            throw new InvalidOperationException(
                $"No connection configured for {typeof(TContext).Name}. Call UseSqlServer/UseOracle/etc.");

        // options – generic olduğu üçün bu context-ə özünəməxsusdur
        services.AddSingleton(options);

        services.Add(
            new ServiceDescriptor(typeof(TContext), typeof(TContext), lifetime));

        return services;
    }
}
