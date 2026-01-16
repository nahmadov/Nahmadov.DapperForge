using Nahmadov.DapperForge.Core.Builders;
using Nahmadov.DapperForge.Core.Common;
using Nahmadov.DapperForge.Core.Context;

namespace ConnectionSample;

public class AppDapperDbContext(DapperDbContextOptions<AppDapperDbContext> options) : DapperDbContext(options)
{
    public DapperSet<Customer> Customers => Set<Customer>();
    public DapperSet<SupportTicket> Tickets => Set<SupportTicket>();
    public DapperSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DapperSet<Product> Products => Set<Product>();

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

            // Configure foreign key relationship using fluent API
            b.HasOne<Customer>(t => t.Customer)
             .WithMany(c => c.SupportTickets)
             .HasForeignKey(t => t.CustomerId);
        });

        modelBuilder.Entity<AuditLog>(b =>
        {
            b.Property(a => a.Entity).HasMaxLength(100);
            b.Property(a => a.Action).HasMaxLength(50);
            b.Property(a => a.Details).HasMaxLength(200);
        });

        // Product entity with composite alternate key (no primary key)
        // Demonstrates multi-tenant scenario where products are unique within tenant by code
        modelBuilder.Entity<Product>(b =>
        {
            b.ToTable("Products", modelBuilder.DefaultSchema);
            b.HasNoKey(); // No primary key
            b.HasAlternateKey(p => new { p.TenantId, p.ProductCode }); // Composite business key

            b.Property(p => p.ProductCode).HasMaxLength(50).IsRequired();
            b.Property(p => p.Name).HasMaxLength(200).IsRequired();
            b.Property(p => p.Description).HasMaxLength(500);
        });
    }
}
