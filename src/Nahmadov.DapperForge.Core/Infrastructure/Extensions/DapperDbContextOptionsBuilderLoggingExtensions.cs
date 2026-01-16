using Nahmadov.DapperForge.Core.Context.Options;
using Nahmadov.DapperForge.Core.Context;

namespace Nahmadov.DapperForge.Core.Infrastructure.Extensions;
/// <summary>
/// Logging-related configuration extensions for <see cref="DapperDbContext"/>.
/// </summary>
public static class DapperDbContextOptionsBuilderLoggingExtensions
{
    /// <summary>
    /// Enables or disables SQL logging to the console.
    /// </summary>
    public static DapperDbContextOptionsBuilder<TContext> EnableSqlLogging<TContext>(
        this DapperDbContextOptionsBuilder<TContext> builder,
        bool enabled = true)
        where TContext : DapperDbContext
    {
        builder.Options.EnableSqlLogging = enabled;
        return builder;
    }
}


