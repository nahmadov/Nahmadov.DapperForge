using System.Data;

using DapperToolkit.Core.Context;

namespace DapperToolkit.Core.Common;

public sealed class DapperDbContextOptionsBuilder<TContext> where TContext : DapperDbContext
{
    internal DapperDbContextOptions<TContext> Options { get; }
    internal DapperDbContextOptionsBuilder(DapperDbContextOptions<TContext> options) => Options = options;
}