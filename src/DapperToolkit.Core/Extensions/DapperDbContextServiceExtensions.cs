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
        where TContext : DapperDbContextBase<TContext>
    {
        // options obyektini yaradıb builder-ə veririk
        var options = new DapperDbContextOptions<TContext>();
        var builder = new DapperDbContextOptionsBuilder<TContext>(options);

        configure(builder);

        if (options.ProviderFactory is null)
        {
            throw new InvalidOperationException(
                $"No Dapper provider has been configured for {typeof(TContext).Name}. " +
                "Call UseSqlServer / UseOracle / UsePostgres, etc.");
        }

        // options – generic olduğu üçün bu context-ə özünəməxsusdur
        services.AddSingleton(options);

        // provider TContext-ə bağlıdır
        services.Add(new ServiceDescriptor(
            typeof(IDapperConnectionProvider<TContext>),
            sp => options.ProviderFactory(sp),
            lifetime));

        // context
        services.Add(new ServiceDescriptor(
            typeof(TContext),
            typeof(TContext),
            lifetime));

        // ümumi IDapperDbContext kimi də inject oluna bilsin
        services.Add(new ServiceDescriptor(
            typeof(IDapperDbContext),
            sp => sp.GetRequiredService<TContext>(),
            lifetime));

        return services;
    }
}
