using System.Data;

using DapperToolkit.Core.Context;
using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.Core.Common;

public class DapperDbContextOptions
{
    internal Func<IDbConnection>? ConnectionFactory { get; set; }
    internal ISqlDialect? Dialect { get; set; }
}

public sealed class DapperDbContextOptions<TContext> : DapperDbContextOptions
    where TContext : DapperDbContext
{
}