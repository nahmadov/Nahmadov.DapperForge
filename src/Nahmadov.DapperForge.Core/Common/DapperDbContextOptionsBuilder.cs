using Nahmadov.DapperForge.Core.Context;

namespace Nahmadov.DapperForge.Core.Common;

/// <summary>
/// Provides fluent configuration for <see cref="DapperDbContext"/> options.
/// </summary>
public sealed class DapperDbContextOptionsBuilder<TContext> where TContext : DapperDbContext
{
    /// <summary>
    /// Gets the options being configured for the context type.
    /// </summary>
    internal DapperDbContextOptions<TContext> Options { get; }

    /// <summary>
    /// Initializes a new builder for the given options instance.
    /// </summary>
    /// <param name="options">Options object to configure.</param>
    internal DapperDbContextOptionsBuilder(DapperDbContextOptions<TContext> options) => Options = options;
}
