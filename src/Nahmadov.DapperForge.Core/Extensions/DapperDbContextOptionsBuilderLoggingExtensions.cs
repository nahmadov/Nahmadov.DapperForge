using Nahmadov.DapperForge.Core.Common;
using Nahmadov.DapperForge.Core.Context;

namespace Nahmadov.DapperForge.Core.Extensions;

/// <summary>
/// Logging-related configuration extensions for <see cref="DapperDbContext"/>.
/// </summary>
public static class DapperDbContextOptionsBuilderLoggingExtensions
{
    /// <summary>
    /// Enables or disables SQL logging to the console.
    /// </summary>
    /// <typeparam name="TContext">Context type being configured.</typeparam>
    /// <param name="builder">Options builder instance.</param>
    /// <param name="enabled">True to log executed SQL, false to silence.</param>
    /// <returns>The original builder for chaining.</returns>
    public static DapperDbContextOptionsBuilder<TContext> EnableSqlLogging<TContext>(
        this DapperDbContextOptionsBuilder<TContext> builder,
        bool enabled = true)
        where TContext : DapperDbContext
    {
        builder.Options.EnableSqlLogging = enabled;
        return builder;
    }
}
