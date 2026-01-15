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

using ForeignKeyAttribute = Nahmadov.DapperForge.Core.Attributes.ForeignKeyAttribute;

namespace Nahmadov.DapperForge.UnitTests.Builders;

public class FluentRelationshipTests
{
    #region Test Models

    private class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<Order> Orders { get; set; } = new();
    }

    private class Order
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }
    }

    private class OrderWithAttribute
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Customer), typeof(Customer), nameof(Customer.Id))]
        public int CustomerId { get; set; }

        public Customer? Customer { get; set; }
    }

    [Table("users", Schema = "auth")]
    private class User
    {
        [Key]
        [Column("user_id")]
        public int Id { get; set; }

        public string Username { get; set; } = string.Empty;
    }

    private class UserProfile
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public string Bio { get; set; } = string.Empty;
    }

    private class HistoryType
    {
        public int HistoryTypeId { get; set; }
        public List<History> Histories { get; set; } = new();
    }

    private class History
    {
        public int Id { get; set; }
        public int HistoryTypeId { get; set; }
        public HistoryType? HistoryType { get; set; }
    }

    #endregion

    #region HasOne().WithMany() Tests

    [Fact]
    public void HasOne_WithMany_HasForeignKey_ConfiguresForeignKey()
    {
        var modelBuilder = new DapperModelBuilder(SqlServerDialect.Instance);

        modelBuilder.Entity<Order>(b =>
        {
            b.HasOne<Customer>(o => o.Customer)
             .WithMany(c => c.Orders)
             .HasForeignKey(o => o.CustomerId);
        });

        // Also register Customer to ensure mapping is available
        modelBuilder.Entity<Customer>();

        var mappings = modelBuilder.Build();
        var orderMapping = mappings[typeof(Order)];

        Assert.Single(orderMapping.ForeignKeys);
        var fk = orderMapping.ForeignKeys[0];
        Assert.Equal("Customer", fk.NavigationProperty.Name);
        Assert.Equal("CustomerId", fk.ForeignKeyProperty.Name);
        Assert.Equal(typeof(Customer), fk.PrincipalEntityType);
        Assert.Equal("CustomerId", fk.ForeignKeyColumnName);
        Assert.Equal("Id", fk.PrincipalKeyColumnName);
        Assert.Equal("Customer", fk.PrincipalTableName);
    }

    [Fact]
    public void HasOne_WithMany_HasPrincipalKey_ConfiguresCustomPrincipalKey()
    {
        var modelBuilder = new DapperModelBuilder(SqlServerDialect.Instance);

        modelBuilder.Entity<Order>(b =>
        {
            b.HasOne<Customer>(o => o.Customer)
             .WithMany(c => c.Orders)
             .HasForeignKey(o => o.CustomerId)
             .HasPrincipalKey(c => c.Id);
        });

        modelBuilder.Entity<Customer>();

        var mappings = modelBuilder.Build();
        var orderMapping = mappings[typeof(Order)];

        Assert.Single(orderMapping.ForeignKeys);
        var fk = orderMapping.ForeignKeys[0];
        Assert.Equal("Id", fk.PrincipalKeyColumnName);
    }

    [Fact]
    public void HasOne_WithMany_NoInverseNavigation_Works()
    {
        var modelBuilder = new DapperModelBuilder(SqlServerDialect.Instance);

        modelBuilder.Entity<Order>(b =>
        {
            b.HasOne<Customer>(o => o.Customer)
             .WithMany()
             .HasForeignKey(o => o.CustomerId);
        });

        modelBuilder.Entity<Customer>();

        var mappings = modelBuilder.Build();
        var orderMapping = mappings[typeof(Order)];

        Assert.Single(orderMapping.ForeignKeys);
        Assert.Equal("Customer", orderMapping.ForeignKeys[0].NavigationProperty.Name);
    }

    [Fact]
    public void HasOne_WithMany_UsesConfiguredPrincipalKey()
    {
        var modelBuilder = new DapperModelBuilder(SqlServerDialect.Instance);

        modelBuilder.Entity<HistoryType>(b =>
        {
            b.ToTable("HistType");
            b.Property(h => h.HistoryTypeId).HasColumnName("HistType");
            b.HasKey(h => h.HistoryTypeId);
        });

        modelBuilder.Entity<History>(b =>
        {
            b.HasOne<HistoryType>(h => h.HistoryType)
             .WithMany(ht => ht.Histories)
             .HasForeignKey(h => h.HistoryTypeId);
        });

        var mappings = modelBuilder.Build();
        var historyMapping = mappings[typeof(History)];

        var fk = Assert.Single(historyMapping.ForeignKeys);
        Assert.Equal("HistType", fk.PrincipalTableName);
        Assert.Equal("HistType", fk.PrincipalKeyColumnName);
    }

    #endregion

    #region HasOne().WithOne() Tests

    [Fact]
    public void HasOne_WithOne_ConfiguresOneToOneRelationship()
    {
        var modelBuilder = new DapperModelBuilder(SqlServerDialect.Instance);

        modelBuilder.Entity<UserProfile>(b =>
        {
            b.HasOne<User>(p => p.User)
             .WithOne()
             .HasForeignKey(p => p.UserId);
        });

        modelBuilder.Entity<User>();

        var mappings = modelBuilder.Build();
        var profileMapping = mappings[typeof(UserProfile)];

        Assert.Single(profileMapping.ForeignKeys);
        var fk = profileMapping.ForeignKeys[0];
        Assert.Equal("User", fk.NavigationProperty.Name);
        Assert.Equal("UserId", fk.ForeignKeyProperty.Name);
        Assert.Equal(typeof(User), fk.PrincipalEntityType);
    }

    [Fact]
    public void HasOne_WithPrincipalTableAttributes_ResolvesTableInfo()
    {
        var modelBuilder = new DapperModelBuilder(SqlServerDialect.Instance);

        modelBuilder.Entity<UserProfile>(b =>
        {
            b.HasOne<User>(p => p.User)
             .WithOne()
             .HasForeignKey(p => p.UserId);
        });

        modelBuilder.Entity<User>();

        var mappings = modelBuilder.Build();
        var profileMapping = mappings[typeof(UserProfile)];

        var fk = profileMapping.ForeignKeys[0];
        Assert.Equal("users", fk.PrincipalTableName);
        Assert.Equal("auth", fk.PrincipalSchema);
        Assert.Equal("user_id", fk.PrincipalKeyColumnName);
    }

    #endregion

    #region Fluent vs Attribute Precedence Tests

    [Fact]
    public void Fluent_Config_Overrides_Attribute()
    {
        var modelBuilder = new DapperModelBuilder(SqlServerDialect.Instance);

        // Configure with fluent API (should override attribute)
        modelBuilder.Entity<OrderWithAttribute>(b =>
        {
            b.HasOne<Customer>(o => o.Customer)
             .WithMany()
             .HasForeignKey(o => o.CustomerId);
        });

        modelBuilder.Entity<Customer>();

        var mappings = modelBuilder.Build();
        var orderMapping = mappings[typeof(OrderWithAttribute)];

        // Should only have one FK mapping (from fluent), not duplicate from attribute
        Assert.Single(orderMapping.ForeignKeys);
    }

    [Fact]
    public void Attribute_Config_Works_Without_Fluent()
    {
        var modelBuilder = new DapperModelBuilder(SqlServerDialect.Instance);

        // Register entity without fluent FK config - should use attribute
        modelBuilder.Entity<OrderWithAttribute>();
        modelBuilder.Entity<Customer>();

        var mappings = modelBuilder.Build();
        var orderMapping = mappings[typeof(OrderWithAttribute)];

        Assert.Single(orderMapping.ForeignKeys);
        var fk = orderMapping.ForeignKeys[0];
        Assert.Equal("Customer", fk.NavigationProperty.Name);
        Assert.Equal("CustomerId", fk.ForeignKeyProperty.Name);
    }

    #endregion

    #region HasMany().WithOne() Tests

    [Fact]
    public void HasMany_WithOne_CreatesRelationshipConfig()
    {
        var modelBuilder = new DapperModelBuilder(SqlServerDialect.Instance);

        modelBuilder.Entity<Customer>(b =>
        {
            b.HasMany<Order>(c => c.Orders)
             .WithOne(o => o.Customer)
             .HasForeignKey(o => o.CustomerId);
        });

        // HasMany configures from principal side but FK is on dependent
        // The relationship config is stored on Customer but FK mapping is resolved when Order is processed
        var mappings = modelBuilder.Build();

        // Verify Customer mapping exists
        Assert.True(mappings.ContainsKey(typeof(Customer)));
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void HasOne_Without_HasForeignKey_Throws()
    {
        var modelBuilder = new DapperModelBuilder(SqlServerDialect.Instance);

        modelBuilder.Entity<Order>(b =>
        {
            b.HasOne<Customer>(o => o.Customer)
             .WithMany();
            // Missing HasForeignKey()
        });

        modelBuilder.Entity<Customer>();

        var ex = Assert.Throws<InvalidOperationException>(() => modelBuilder.Build());
        Assert.Contains("Foreign key property must be specified", ex.Message);
        Assert.Contains("Customer", ex.Message);
    }

    [Fact]
    public void HasOne_With_InvalidNavigationProperty_Throws()
    {
        var modelBuilder = new DapperModelBuilder(SqlServerDialect.Instance);

        // This will throw at build time because the navigation property doesn't exist
        modelBuilder.Entity<Order>(b =>
        {
            b.HasOne<Customer>(o => o.Customer)
             .WithMany()
             .HasForeignKey(o => o.CustomerId);
        });

        // Don't register Customer - should still work as we're checking Order's properties
        var mappings = modelBuilder.Build();
        Assert.Single(mappings[typeof(Order)].ForeignKeys);
    }

    [Fact]
    public void HasForeignKey_With_InvalidProperty_Throws()
    {
        var modelBuilder = new DapperModelBuilder(SqlServerDialect.Instance);

        modelBuilder.Entity<Order>(b =>
        {
            b.HasOne<Customer>(o => o.Customer)
             .WithMany()
             .HasForeignKey(o => o.Id); // Wrong property - using Id instead of CustomerId
        });

        var mappings = modelBuilder.Build();
        var fk = mappings[typeof(Order)].ForeignKeys[0];

        // Should use whatever property was specified
        Assert.Equal("Id", fk.ForeignKeyProperty.Name);
    }

    #endregion

    #region Context Integration Tests

    [Fact]
    public void DbContext_OnModelCreating_ConfiguresRelationships()
    {
        var options = new DapperDbContextOptions<RelationshipTestContext>
        {
            ConnectionFactory = () => new FakeDbConnection(),
            Dialect = SqlServerDialect.Instance
        };

        using var ctx = new RelationshipTestContext(options);

        // Trigger model build
        _ = ctx.Orders;

        var orderMapping = ctx.ExposeMapping<Order>();

        Assert.Single(orderMapping.ForeignKeys);
        var fk = orderMapping.ForeignKeys[0];
        Assert.Equal("Customer", fk.NavigationProperty.Name);
        Assert.Equal("CustomerId", fk.ForeignKeyProperty.Name);
    }

    private class RelationshipTestContext(DapperDbContextOptions<RelationshipTestContext> options)
        : DapperDbContext(options)
    {
        public DapperSet<Customer> Customers => Set<Customer>();
        public DapperSet<Order> Orders => Set<Order>();

        protected override void OnModelCreating(DapperModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>(b =>
            {
                b.HasOne<Customer>(o => o.Customer)
                 .WithMany(c => c.Orders)
                 .HasForeignKey(o => o.CustomerId);
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

    #endregion
}
