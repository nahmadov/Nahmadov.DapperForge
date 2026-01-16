using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Nahmadov.DapperForge.Core.Modeling.Builders;
using Nahmadov.DapperForge.Core.Querying.Sql;
using Nahmadov.DapperForge.Core.Context.Options;
using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.SqlServer;
using Nahmadov.DapperForge.UnitTests.Fakes;

namespace Nahmadov.DapperForge.UnitTests.Context;

public partial class DapperSetAdvancedMutationTests
{
    [Table("Employees", Schema = "dbo")]
    private class Employee
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string EmployeeNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Department { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Location { get; set; }

        public string Status { get; set; } = "Active";

        public decimal Salary { get; set; }

        public bool IsTemporary { get; set; }
    }

    [Table("LegacyProducts", Schema = "dbo")]
    private class LegacyProduct
    {
        [Required]
        [StringLength(50)]
        public string ProductCode { get; set; } = string.Empty; // Alternate Key

        [Required]
        [StringLength(100)]
        public string ProductName { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public string? Category { get; set; }
    }

    [Table("LogEntries", Schema = "dbo")]
    private class LogEntry
    {
        [Required]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; }

        public string? Level { get; set; }
    }

    private static (TestDapperDbContext ctx, FakeDbConnection conn) CreateContext()
    {
        var conn = new FakeDbConnection();
        var options = new DapperDbContextOptions<TestDapperDbContext>
        {
            ConnectionFactory = () => conn,
            Dialect = SqlServerDialect.Instance
        };

        var ctx = new TestDapperDbContext(options);
        return (ctx, conn);
    }

    private static DapperSet<Employee> GetEmployeeSet(TestDapperDbContext ctx)
    {
        var builder = new DapperModelBuilder(SqlServerDialect.Instance);
        builder.Entity<Employee>(b =>
        {
            b.ToTable("Employees", "dbo");
            b.Property(e => e.EmployeeNumber).IsRequired().HasMaxLength(50);
            b.Property(e => e.Name).IsRequired().HasMaxLength(100);
            b.Property(e => e.Department).IsRequired().HasMaxLength(50);
            b.Property(e => e.Location).HasMaxLength(50);
        });

        var model = builder.Build();
        var mapping = model[typeof(Employee)];
        var generator = new SqlGenerator<Employee>(SqlServerDialect.Instance, mapping);

        return new DapperSet<Employee>(ctx, generator, mapping);
    }

    private static DapperSet<LegacyProduct> GetLegacyProductSet(TestDapperDbContext ctx)
    {
        var builder = new DapperModelBuilder(SqlServerDialect.Instance);
        builder.Entity<LegacyProduct>(b =>
        {
            b.ToTable("LegacyProducts", "dbo");
            b.HasNoKey(); // No primary key
            b.HasAlternateKey(p => p.ProductCode); // Business key
            b.Property(p => p.ProductCode).IsRequired().HasMaxLength(50);
            b.Property(p => p.ProductName).IsRequired().HasMaxLength(100);
        });

        var model = builder.Build();
        var mapping = model[typeof(LegacyProduct)];
        var generator = new SqlGenerator<LegacyProduct>(SqlServerDialect.Instance, mapping);

        return new DapperSet<LegacyProduct>(ctx, generator, mapping);
    }

    private static DapperSet<LogEntry> GetLogEntrySet(TestDapperDbContext ctx)
    {
        var builder = new DapperModelBuilder(SqlServerDialect.Instance);
        builder.Entity<LogEntry>(b =>
        {
            b.ToTable("LogEntries", "dbo");
            b.HasNoKey(); // No primary key and no alternate key
            b.Property(e => e.Message).IsRequired().HasMaxLength(500);
        });

        var model = builder.Build();
        var mapping = model[typeof(LogEntry)];
        var generator = new SqlGenerator<LogEntry>(SqlServerDialect.Instance, mapping);

        return new DapperSet<LogEntry>(ctx, generator, mapping);
    }
}
