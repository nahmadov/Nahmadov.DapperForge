using DapperToolkit.Core.Common;
using DapperToolkit.Core.Context;

using Oracle.ManagedDataAccess.Client;

namespace DapperToolkit.Oracle.Extensions;

public static class DapperOracleOptionsBuilderExtensions
{
    public static DapperDbContextOptionsBuilder<TContext> UseOracle<TContext>(
        this DapperDbContextOptionsBuilder<TContext> builder,
        string connectionString)
        where TContext : DapperDbContext
    {
        builder.Options.ConnectionFactory = () => new OracleConnection(connectionString);
        return builder;
    }
}