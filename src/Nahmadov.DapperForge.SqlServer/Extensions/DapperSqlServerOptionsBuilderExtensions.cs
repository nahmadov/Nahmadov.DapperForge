using Microsoft.Data.SqlClient;

using Nahmadov.DapperForge.Core.Context.Options;
using Nahmadov.DapperForge.Core.Context;

namespace Nahmadov.DapperForge.SqlServer.Extensions;

/// <summary>
/// SQL Server-specific extensions for configuring a <see cref="DapperDbContext"/>.
/// </summary>
public static class DapperSqlServerOptionsBuilderExtensions
{
    /// <summary>
    /// Configures the context to use SQL Server with the provided connection string.
    /// </summary>
    public static DapperDbContextOptionsBuilder<TContext> UseSqlServer<TContext>(
        this DapperDbContextOptionsBuilder<TContext> builder,
        string connectionString)
        where TContext : DapperDbContext
    {
        builder.Options.ConnectionFactory = () => new SqlConnection(connectionString);
        builder.Options.Dialect = SqlServerDialect.Instance;
        return builder;
    }
}

