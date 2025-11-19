using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.Core.Common;

public sealed class DapperDbContextOptions<TContext> where TContext : IDapperDbContext
{
    internal Func<IServiceProvider, IDapperConnectionProvider<TContext>>? ProviderFactory { get; set; }
}