using System.Data;

using Microsoft.Extensions.Logging;

using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.Core.Abstractions;

namespace Nahmadov.DapperForge.Core.Context.Options;
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

    /// <summary>
    /// Maximum number of retry attempts for transient failures.
    /// Default is 3. Set to 0 to disable retries.
    /// </summary>
    internal int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// Base delay for exponential backoff retry strategy in milliseconds.
    /// Default is 100ms. Actual delay is baseDelay * 2^(attempt-1).
    /// </summary>
    internal int RetryDelayMilliseconds { get; set; } = 100;

    /// <summary>
    /// Command timeout in seconds. Default is 30 seconds.
    /// Set to 0 for no timeout (not recommended).
    /// </summary>
    internal int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Enables connection health checks before executing commands.
    /// Default is false for performance.
    /// </summary>
    internal bool EnableConnectionHealthCheck { get; set; }
}

/// <summary>
/// Context-specific options for a typed <see cref="DapperDbContext"/>.
/// </summary>
public sealed class DapperDbContextOptions<TContext> : DapperDbContextOptions
    where TContext : DapperDbContext
{
}


