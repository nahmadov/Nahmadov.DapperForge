using System.Data;

using Microsoft.Extensions.Logging;

using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.Core.Interfaces;

namespace Nahmadov.DapperForge.Core.Common;

/// <summary>
/// Represents configuration options for a <see cref="DapperDbContext"/>.
/// </summary>
public class DapperDbContextOptions
{
    internal Func<IDbConnection>? ConnectionFactory { get; set; }
    internal ISqlDialect? Dialect { get; set; }

    /// <summary>
    /// Enables basic SQL logging to console.
    /// Use <see cref="Logger"/> for more advanced logging with ILogger integration.
    /// </summary>
    internal bool EnableSqlLogging { get; set; }

    /// <summary>
    /// Optional logger for structured logging of SQL commands and operations.
    /// When set, this takes precedence over <see cref="EnableSqlLogging"/>.
    /// </summary>
    internal ILogger? Logger { get; set; }
}

/// <summary>
/// Context-specific options for a typed <see cref="DapperDbContext"/>.
/// </summary>
public sealed class DapperDbContextOptions<TContext> : DapperDbContextOptions
    where TContext : DapperDbContext
{
}
