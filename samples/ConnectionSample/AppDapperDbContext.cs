using Nahmadov.DapperForge.Core.Builders;
using Nahmadov.DapperForge.Core.Common;
using Nahmadov.DapperForge.Core.Context;

namespace ConnectionSample;

public class AppDapperDbContext(DapperDbContextOptions<AppDapperDbContext> options) : DapperDbContext(options)
{
    public DapperSet<Customer> Customers => Set<Customer>();
    public DapperSet<SupportTicket> Tickets => Set<SupportTicket>();
    public DapperSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(DapperModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(b =>
        {
            b.Property(c => c.Name).HasColumnName("FullName").HasMaxLength(120).IsRequired();
            b.Property(c => c.Email).HasMaxLength(200);
            b.Property(c => c.City).HasMaxLength(100);
            b.Property(c => c.LastLogin).IsReadOnly();
        });

        modelBuilder.Entity<SupportTicket>(b =>
        {
            b.ToTable("SupportTickets", modelBuilder.DefaultSchema);
            b.Property(t => t.Title).HasMaxLength(200).IsRequired();
            b.Property(t => t.Description).HasMaxLength(500);
            b.Property(t => t.Status).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<AuditLog>(b =>
        {
            b.Property(a => a.Entity).HasMaxLength(100);
            b.Property(a => a.Action).HasMaxLength(50);
            b.Property(a => a.Details).HasMaxLength(200);
        });
    }
}
