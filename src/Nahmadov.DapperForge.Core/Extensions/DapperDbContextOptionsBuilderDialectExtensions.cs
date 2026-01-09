using Nahmadov.DapperForge.Core.Common;
using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.Core.Interfaces;

namespace Nahmadov.DapperForge.Core.Extensions;

/// <summary>
/// Extension methods for configuring SQL dialects on Dapper context options.
/// </summary>
internal static class DapperDbContextOptionsBuilderDialectExtensions
{
    /// <summary>
    /// Sets the SQL dialect for the context.
    /// </summary>
    internal static DapperDbContextOptionsBuilder<TContext> UseDialect<TContext>(
        this DapperDbContextOptionsBuilder<TContext> builder,
        ISqlDialect dialect)
        where TContext : DapperDbContext
    {
        ArgumentNullException.ThrowIfNull(dialect);

        builder.Options.Dialect = dialect;
        return builder;
    }
}
