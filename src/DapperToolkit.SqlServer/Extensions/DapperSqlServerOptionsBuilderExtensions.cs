using DapperToolkit.Core.Common;
using DapperToolkit.Core.Context;

namespace DapperToolkit.SqlServer.Extensions;

public static class DapperSqlServerOptionsBuilderExtensions
{
    public static DapperDbContextOptionsBuilder<TContext> UseSqlServer<TContext>(
        this DapperDbContextOptionsBuilder<TContext> builder,
        string connectionString)
        where TContext : DapperDbContextBase<TContext>
    {
        return builder.UseProvider(_ =>
            new SqlServerConnectionProvider<TContext>(connectionString));
    }
}