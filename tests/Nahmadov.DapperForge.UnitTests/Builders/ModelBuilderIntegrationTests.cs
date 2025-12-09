using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

using Nahmadov.DapperForge.Core.Builders;
using Nahmadov.DapperForge.Core.Common;
using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.Core.Mapping;
using Nahmadov.DapperForge.SqlServer;
using Nahmadov.DapperForge.UnitTests.Fakes;
using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Builders;

public class ModelBuilderIntegrationTests
{
    [Fact]
    public void OnModelCreating_Fluent_Configures_Mapping_And_Generator()
    {
        var options = new DapperDbContextOptions<ConfiguredContext>
        {
            ConnectionFactory = () => new FakeDbConnection(),
            Dialect = SqlServerDialect.Instance
        };

        using var ctx = new ConfiguredContext(options);

        // Trigger model build
        _ = ctx.Users;

        var mapping = ctx.ExposeMapping<User>();

        Assert.Equal("tbl_users", mapping.TableName);
        Assert.Equal("custom", mapping.Schema);

        var nameMap = mapping.PropertyMappings.Single(pm => pm.Property.Name == nameof(User.Name));
        Assert.Equal("full_name", nameMap.ColumnName);
        Assert.True(nameMap.IsRequired);
        Assert.Equal(50, nameMap.MaxLength);

        var generator = new SqlGenerator<User>(SqlServerDialect.Instance, mapping);
        Assert.Contains("[full_name]", generator.InsertSql);
        Assert.Contains("@Name", generator.InsertSql);
    }

    private class ConfiguredContext(DapperDbContextOptions<ConfiguredContext> options) : DapperDbContext(options)
    {
        public DapperSet<User> Users => Set<User>();

        protected override void OnModelCreating(DapperModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(b =>
            {
                b.ToTable("tbl_users", "custom");
                b.HasKey(u => u.Id);
                b.Property(u => u.Name).HasColumnName("full_name").IsRequired().HasMaxLength(50);
            });
        }

        public EntityMapping ExposeMapping<TEntity>() where TEntity : class
        {
            var method = typeof(DapperDbContext)
                .GetMethod("GetEntityMapping", BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(typeof(TEntity));

            return (EntityMapping)method.Invoke(this, null)!;
        }
    }

    [Table("users")]
    private class User
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
