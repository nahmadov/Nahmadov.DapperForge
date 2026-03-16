using Microsoft.Data.Sqlite;

using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.Core.Context.Options;

namespace Nahmadov.DapperForge.Sqlite.Extensions;

/// <summary>
/// SQLite-specific extensions for configuring a <see cref="DapperDbContext"/>.
/// </summary>
public static class DapperSqliteOptionsBuilderExtensions
{
    /// <summary>
    /// Configures the context to use SQLite with the provided connection string.
    /// </summary>
    public static DapperDbContextOptionsBuilder<TContext> UseSqlite<TContext>(
        this DapperDbContextOptionsBuilder<TContext> builder,
        string connectionString)
        where TContext : DapperDbContext
    {
        builder.Options.ConnectionFactory = () => new SqliteConnection(connectionString);
        builder.Options.Dialect = SqliteDialect.Instance;
        return builder;
    }
}
