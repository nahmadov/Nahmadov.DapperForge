using System.Data;

using DapperToolkit.Core.Context;

namespace DapperToolkit.Core.Common;

public sealed class DapperDbContextOptionsBuilder<TContext> where TContext : DapperDbContext
{
    internal DapperDbContextOptions<TContext> Options { get; }
    internal DapperDbContextOptionsBuilder(DapperDbContextOptions<TContext> options) => Options = options;

    public DapperDbContextOptionsBuilder<TContext> UseProvider(Func<IDbConnection> connectionFactory)
    {
        Options.ConnectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
        return this;
    }
}