using Nahmadov.DapperForge.Core.Modeling.Builders;
using Nahmadov.DapperForge.Core.Context.Options;
using Nahmadov.DapperForge.Core.Context;

namespace ConnectionSample;

public class AppDapperDbContext(DapperDbContextOptions<AppDapperDbContext> options) : DapperDbContext(options)
{
    public DapperSet<Customer> Customers => Set<Customer>();
    public DapperSet<SupportTicket> Tickets => Set<SupportTicket>();
    public DapperSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DapperSet<Product> Products => Set<Product>();

    // ThenInclude sample sets
    public DapperSet<Department> Departments => Set<Department>();
    public DapperSet<Employee> Employees => Set<Employee>();
    public DapperSet<EmployeeAddress> EmployeeAddresses => Set<EmployeeAddress>();
    public DapperSet<Assignment> Assignments => Set<Assignment>();
    public DapperSet<AssignmentCategory> AssignmentCategories => Set<AssignmentCategory>();

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

        // ── ThenInclude sample entities ──────────────────────────────────────

        modelBuilder.Entity<Department>(b =>
        {
            b.ToTable("Departments", modelBuilder.DefaultSchema);
            b.Property(d => d.Name).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<EmployeeAddress>(b =>
        {
            b.ToTable("EmployeeAddresses", modelBuilder.DefaultSchema);
            b.Property(a => a.Street).HasMaxLength(200);
            b.Property(a => a.City).HasMaxLength(100);
        });

        modelBuilder.Entity<Employee>(b =>
        {
            b.ToTable("Employees", modelBuilder.DefaultSchema);
            b.Property(e => e.Position).HasMaxLength(100);

            // B → A  (Employee belongs to Department)
            b.HasOne<Department>(e => e.Department)
             .WithMany(d => d.Employees)
             .HasForeignKey(e => e.DepartmentId);

            // B → C  (Employee has one EmployeeAddress; FK AddressId is on Employee)
            b.HasOne<EmployeeAddress>(e => e.Address)
             .WithMany()
             .HasForeignKey(e => e.AddressId);
        });

        modelBuilder.Entity<AssignmentCategory>(b =>
        {
            b.ToTable("AssignmentCategories", modelBuilder.DefaultSchema);
            b.Property(c => c.Name).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<Assignment>(b =>
        {
            b.ToTable("Assignments", modelBuilder.DefaultSchema);
            b.Property(a => a.Title).HasMaxLength(200).IsRequired();

            // D → B  (Assignment belongs to Employee)
            b.HasOne<Employee>(a => a.Employee)
             .WithMany(e => e.Assignments)
             .HasForeignKey(a => a.EmployeeId);

            // D → F  (Assignment has one AssignmentCategory)
            b.HasOne<AssignmentCategory>(a => a.Category)
             .WithMany()
             .HasForeignKey(a => a.CategoryId);
        });

        // ─────────────────────────────────────────────────────────────────────

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

