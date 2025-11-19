using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.Core.Common;

public sealed class DapperDbContextOptionsBuilder<TContext> where TContext : IDapperDbContext
{
    internal DapperDbContextOptions<TContext> Options { get; }

    internal DapperDbContextOptionsBuilder(DapperDbContextOptions<TContext> options)
        => Options = options;

    internal DapperDbContextOptionsBuilder<TContext> UseProvider(
        Func<IServiceProvider, IDapperConnectionProvider<TContext>> factory)
    {
        Options.ProviderFactory = factory;
        return this;
    }
}