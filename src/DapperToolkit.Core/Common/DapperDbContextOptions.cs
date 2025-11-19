using System.Data;

using DapperToolkit.Core.Context;

namespace DapperToolkit.Core.Common;

public class DapperDbContextOptions
{
    internal Func<IDbConnection>? ConnectionFactory { get; set; }
}

public sealed class DapperDbContextOptions<TContext> : DapperDbContextOptions
    where TContext : DapperDbContext
{
}