using DapperToolkit.Core.Builders;
using DapperToolkit.Core.Common;
using DapperToolkit.Core.Context;

namespace ConnectionSample;


public class AppDapperDbContext(DapperDbContextOptions<AppDapperDbContext> options) : DapperDbContext(options)
{
    public DapperSet<User> Users => Set<User>();
    public DapperSet<LogEntry> Logs => Set<LogEntry>();

    protected override void OnModelCreating(DapperModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.Property(u => u.Name).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<LogEntry>(b =>
        {
            b.Property(l => l.Message).HasMaxLength(256);
        });
    }
}
