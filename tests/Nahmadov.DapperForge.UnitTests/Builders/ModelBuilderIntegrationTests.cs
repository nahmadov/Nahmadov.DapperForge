using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

using Nahmadov.DapperForge.Core.Modeling.Builders;
using Nahmadov.DapperForge.Core.Querying.Sql;
using Nahmadov.DapperForge.Core.Context.Options;
using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;
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
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .First(m => m.Name == "GetEntityMapping" && m.IsGenericMethodDefinition)
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

    [Fact]
    public void ApplyConfigurationsFromAssembly_Applies_All_Configurations()
    {
        var options = new DapperDbContextOptions<ConfiguredWithAssemblyContext>
        {
            ConnectionFactory = () => new FakeDbConnection(),
            Dialect = SqlServerDialect.Instance
        };

        using var ctx = new ConfiguredWithAssemblyContext(options);

        // Trigger model build
        _ = ctx.Products;
        _ = ctx.Categories;

        var productMapping = ctx.ExposeMapping<Product>();
        var categoryMapping = ctx.ExposeMapping<Category>();

        // Verify Product configuration
        Assert.Equal("products", productMapping.TableName);
        Assert.Equal("catalog", productMapping.Schema);
        var priceMap = productMapping.PropertyMappings.Single(pm => pm.Property.Name == nameof(Product.Price));
        Assert.Equal("unit_price", priceMap.ColumnName);

        // Verify Category configuration
        Assert.Equal("categories", categoryMapping.TableName);
        Assert.Equal("catalog", categoryMapping.Schema);
        var nameMap = categoryMapping.PropertyMappings.Single(pm => pm.Property.Name == nameof(Category.Name));
        Assert.Equal("category_name", nameMap.ColumnName);
        Assert.True(nameMap.IsRequired);
    }

    [Fact]
    public void ApplyConfigurationsFromAssembly_With_Predicate_Filters_Configurations()
    {
        var options = new DapperDbContextOptions<ConfiguredWithPredicateContext>
        {
            ConnectionFactory = () => new FakeDbConnection(),
            Dialect = SqlServerDialect.Instance
        };

        using var ctx = new ConfiguredWithPredicateContext(options);

        // Trigger model build
        _ = ctx.Products;
        _ = ctx.Categories;

        var productMapping = ctx.ExposeMapping<Product>();
        var categoryMapping = ctx.ExposeMapping<Category>();

        // Product should be configured (ends with Configuration)
        Assert.Equal("products", productMapping.TableName);

        // Category should NOT be configured (doesn't match predicate)
        // So it uses default mapping from attributes or convention
        Assert.NotEqual("catalog", categoryMapping.Schema);
    }

    [Fact]
    public void ApplyConfiguration_Applies_Single_Configuration()
    {
        var modelBuilder = new DapperModelBuilder(SqlServerDialect.Instance);
        var config = new ProductConfiguration();

        modelBuilder.ApplyConfiguration(config);

        var mappings = modelBuilder.Build();
        var productMapping = mappings[typeof(Product)];

        Assert.Equal("products", productMapping.TableName);
        Assert.Equal("catalog", productMapping.Schema);
    }

    private class ConfiguredWithAssemblyContext(DapperDbContextOptions<ConfiguredWithAssemblyContext> options)
        : DapperDbContext(options)
    {
        public DapperSet<Product> Products => Set<Product>();
        public DapperSet<Category> Categories => Set<Category>();

        protected override void OnModelCreating(DapperModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }

        public EntityMapping ExposeMapping<TEntity>() where TEntity : class
        {
            var method = typeof(DapperDbContext)
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .First(m => m.Name == "GetEntityMapping" && m.IsGenericMethodDefinition)
                .MakeGenericMethod(typeof(TEntity));

            return (EntityMapping)method.Invoke(this, null)!;
        }
    }

    private class ConfiguredWithPredicateContext(DapperDbContextOptions<ConfiguredWithPredicateContext> options)
        : DapperDbContext(options)
    {
        public DapperSet<Product> Products => Set<Product>();
        public DapperSet<Category> Categories => Set<Category>();

        protected override void OnModelCreating(DapperModelBuilder modelBuilder)
        {
            // Only apply configurations that end with "Configuration"
            modelBuilder.ApplyConfigurationsFromAssembly(
                Assembly.GetExecutingAssembly(),
                type => type.Name.EndsWith("Configuration"));
        }

        public EntityMapping ExposeMapping<TEntity>() where TEntity : class
        {
            var method = typeof(DapperDbContext)
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .First(m => m.Name == "GetEntityMapping" && m.IsGenericMethodDefinition)
                .MakeGenericMethod(typeof(TEntity));

            return (EntityMapping)method.Invoke(this, null)!;
        }
    }

    // Test entities
    [Table("products")]
    private class Product
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public decimal Price { get; set; }
    }

    [Table("categories")]
    private class Category
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    // Configuration classes
    private class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.ToTable("products", "catalog");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Price).HasColumnName("unit_price");
        }
    }

    private class CategoryConfig : IEntityTypeConfiguration<Category>
    {
        public void Configure(EntityTypeBuilder<Category> builder)
        {
            builder.ToTable("categories", "catalog");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Name).HasColumnName("category_name").IsRequired();
        }
    }
}



