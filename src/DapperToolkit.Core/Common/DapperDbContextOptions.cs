using System.Data;

using DapperToolkit.Core.Context;
using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.Core.Common;

/// <summary>
/// Represents configuration options for a <see cref="DapperDbContext"/>.
/// </summary>
public class DapperDbContextOptions
{
    internal Func<IDbConnection>? ConnectionFactory { get; set; }
    internal ISqlDialect? Dialect { get; set; }
}

/// <summary>
/// Context-specific options for a typed <see cref="DapperDbContext"/>.
/// </summary>
public sealed class DapperDbContextOptions<TContext> : DapperDbContextOptions
    where TContext : DapperDbContext
{
}
