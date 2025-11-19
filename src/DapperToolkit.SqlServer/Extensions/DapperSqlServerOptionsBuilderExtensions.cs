using Microsoft.Data.SqlClient;

using DapperToolkit.Core.Common;
using DapperToolkit.Core.Context;

namespace DapperToolkit.SqlServer.Extensions;

public static class DapperSqlServerOptionsBuilderExtensions
{
    public static DapperDbContextOptionsBuilder<TContext> UseSqlServer<TContext>(
        this DapperDbContextOptionsBuilder<TContext> builder,
        string connectionString)
        where TContext : DapperDbContext
    {
        builder.Options.ConnectionFactory = () => new SqlConnection(connectionString);
        return builder;

    }
}