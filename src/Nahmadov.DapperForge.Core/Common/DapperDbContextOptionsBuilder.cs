using Nahmadov.DapperForge.Core.Context;

namespace Nahmadov.DapperForge.Core.Common;

/// <summary>
/// Provides fluent configuration for <see cref="DapperDbContext"/> options.
/// </summary>
public sealed class DapperDbContextOptionsBuilder<TContext> where TContext : DapperDbContext
{
    internal DapperDbContextOptions<TContext> Options { get; }

    internal DapperDbContextOptionsBuilder(DapperDbContextOptions<TContext> options) => Options = options;
}
