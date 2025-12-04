using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using DapperToolkit.Core.Builders;
using DapperToolkit.Core.Common;
using DapperToolkit.Core.Context;
using DapperToolkit.Core.Mapping;
using DapperToolkit.Oracle;
using DapperToolkit.UnitTests.Fakes;
using Xunit;

namespace DapperToolkit.UnitTests.Integration;

public class OracleCompositeReturningTests
{
    [Fact]
    public void Generates_Returning_For_Composite_Keys()
    {
        var options = new DapperDbContextOptions<OracleCompositeContext>
        {
            ConnectionFactory = () => new FakeDbConnection(),
            Dialect = OracleDialect.Instance
        };

        using var ctx = new OracleCompositeContext(options);
        var set = ctx.Set<CompositeEntity>();

        var mapping = ctx.ExposeMapping<CompositeEntity>();
        var generator = new SqlGenerator<CompositeEntity>(OracleDialect.Instance, mapping);

        Assert.NotNull(generator.InsertReturningIdSql);
        Assert.Contains("RETURNING \"TenantId\", \"UserId\" INTO :TenantId, :UserId", generator.InsertReturningIdSql);
    }

    private class OracleCompositeContext(DapperDbContextOptions<OracleCompositeContext> options) : DapperDbContext(options)
    {
        protected override void OnModelCreating(DapperModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CompositeEntity>(b =>
            {
                b.ToTable("Users", "custom");
                b.HasKey(e => e.TenantId, e => e.UserId);
                b.Property(e => e.TenantId).HasSequence("tenant_seq");
                b.Property(e => e.UserId).HasSequence("user_seq");
                b.Property(e => e.Name).HasColumnName("username");
            });
        }

        public EntityMapping ExposeMapping<TEntity>() where TEntity : class
        {
            var method = typeof(DapperDbContext)
                .GetMethod("GetEntityMapping", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .MakeGenericMethod(typeof(TEntity));

            return (EntityMapping)method.Invoke(this, null)!;
        }
    }

    private class CompositeEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int TenantId { get; set; }

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int UserId { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
