using DapperToolkit.Core.Common;
using DapperToolkit.Core.Context;

namespace DapperToolkit.Oracle.Extensions;

public static class DapperOracleOptionsBuilderExtensions
{
    public static DapperDbContextOptionsBuilder<TContext> UseOracle<TContext>(
        this DapperDbContextOptionsBuilder<TContext> builder,
        string connectionString)
        where TContext : DapperDbContextBase<TContext>
    {
        return builder.UseProvider(_ =>
            new OracleConnectionProvider<TContext>(connectionString));
    }
}