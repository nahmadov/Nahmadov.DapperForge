using DapperToolkit.Core.Common;
using DapperToolkit.Core.Context;
using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.Core.Extensions;

/// <summary>
/// Extension methods for configuring SQL dialects on Dapper context options.
/// </summary>
internal static class DapperDbContextOptionsBuilderDialectExtensions
{
    /// <summary>
    /// Sets the SQL dialect for the context.
    /// </summary>
    /// <typeparam name="TContext">Context type being configured.</typeparam>
    /// <param name="builder">Options builder instance.</param>
    /// <param name="dialect">Dialect to use.</param>
    /// <returns>The original builder for chaining.</returns>
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
