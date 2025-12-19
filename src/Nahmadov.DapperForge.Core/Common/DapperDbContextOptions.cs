using System.Data;

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
    internal bool EnableSqlLogging { get; set; }
}

/// <summary>
/// Context-specific options for a typed <see cref="DapperDbContext"/>.
/// </summary>
public sealed class DapperDbContextOptions<TContext> : DapperDbContextOptions
    where TContext : DapperDbContext
{
}
