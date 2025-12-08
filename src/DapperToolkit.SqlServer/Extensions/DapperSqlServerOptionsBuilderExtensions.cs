using Microsoft.Data.SqlClient;

using DapperToolkit.Core.Common;
using DapperToolkit.Core.Context;

namespace DapperToolkit.SqlServer.Extensions;

/// <summary>
/// SQL Server-specific extensions for configuring a <see cref="DapperDbContext"/>.
/// </summary>
public static class DapperSqlServerOptionsBuilderExtensions
{
    /// <summary>
    /// Configures the context to use SQL Server with the provided connection string.
    /// </summary>
    /// <typeparam name="TContext">Context type.</typeparam>
    /// <param name="builder">Options builder instance.</param>
    /// <param name="connectionString">SQL Server connection string.</param>
    /// <returns>The original builder for chaining.</returns>
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
