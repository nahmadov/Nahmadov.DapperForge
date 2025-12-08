using DapperToolkit.Core.Common;
using DapperToolkit.Core.Context;

using Oracle.ManagedDataAccess.Client;

namespace DapperToolkit.Oracle.Extensions;

/// <summary>
/// Oracle-specific extensions for configuring a <see cref="DapperDbContext"/>.
/// </summary>
public static class DapperOracleOptionsBuilderExtensions
{
    /// <summary>
    /// Configures the context to use Oracle with the given connection string.
    /// </summary>
    /// <typeparam name="TContext">Context type.</typeparam>
    /// <param name="builder">Options builder instance.</param>
    /// <param name="connectionString">Oracle connection string.</param>
    /// <returns>The original builder for chaining.</returns>
    public static DapperDbContextOptionsBuilder<TContext> UseOracle<TContext>(
        this DapperDbContextOptionsBuilder<TContext> builder,
        string connectionString)
        where TContext : DapperDbContext
    {
        builder.Options.ConnectionFactory = () => new OracleConnection(connectionString);
        builder.Options.Dialect = OracleDialect.Instance;
        return builder;
    }
}
