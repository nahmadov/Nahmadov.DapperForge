using DapperToolkit.Core.Common;
using DapperToolkit.Core.Context;
using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.Core.Extensions;

internal static class DapperDbContextOptionsBuilderDialectExtensions
{
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