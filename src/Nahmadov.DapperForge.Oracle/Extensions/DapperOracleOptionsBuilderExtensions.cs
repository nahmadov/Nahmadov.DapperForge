using Nahmadov.DapperForge.Core.Common;
using Nahmadov.DapperForge.Core.Context;

using Oracle.ManagedDataAccess.Client;

namespace Nahmadov.DapperForge.Oracle.Extensions;

/// <summary>
/// Oracle-specific extensions for configuring a <see cref="DapperDbContext"/>.
/// </summary>
public static class DapperOracleOptionsBuilderExtensions
{
    /// <summary>
    /// Configures the context to use Oracle with the given connection string.
    /// </summary>
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
