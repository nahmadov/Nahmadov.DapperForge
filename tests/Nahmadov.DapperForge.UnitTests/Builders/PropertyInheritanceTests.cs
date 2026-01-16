using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Builders;
using Nahmadov.DapperForge.Core.Modeling.Mapping;
using Nahmadov.DapperForge.Core.Querying.Predicates;
using Nahmadov.DapperForge.SqlServer;

using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Builders;

/// <summary>
/// Tests for property inheritance scenarios in PredicateVisitor and OrderingVisitor.
/// Verifies that properties inherited from base classes or interfaces are correctly resolved.
/// </summary>
public class PropertyInheritanceTests
{
    #region Test Entities

    public abstract class BaseEntity
    {
        [Key]
        public int Id { get; set; }

        [Column("entity_name")]
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }

    public interface ITrackable
    {
        bool IsActive { get; set; }
        DateTime? LastModified { get; set; }
    }

    public interface IAuditable
    {
        string CreatedBy { get; set; }
    }

    [Table("DerivedEntities")]
    public class DerivedEntity : BaseEntity, ITrackable
    {
        public string Description { get; set; } = string.Empty;

        // ITrackable implementation
        public bool IsActive { get; set; }
        public DateTime? LastModified { get; set; }
    }

    [Table("MultiInterfaceEntities")]
    public class MultiInterfaceEntity : BaseEntity, ITrackable, IAuditable
    {
        public string Code { get; set; } = string.Empty;

        // ITrackable implementation
        public bool IsActive { get; set; }
        public DateTime? LastModified { get; set; }

        // IAuditable implementation
        public string CreatedBy { get; set; } = string.Empty;
    }

    #endregion

    #region PredicateVisitor - Inherited Property Tests

    [Fact]
    public void PredicateVisitor_InheritedProperty_Name_Works()
    {
        // Arrange
        var (mapping, dialect) = CreateMapping<DerivedEntity>();
        var visitor = new PredicateVisitor<DerivedEntity>(mapping, dialect);

        // Act - Name is inherited from BaseEntity
        var (sql, parameters) = visitor.Translate(e => e.Name == "test");

        // Assert
        Assert.Equal("(a.[entity_name] = @p0)", sql);
        var dict = Assert.IsType<Dictionary<string, object>>(parameters);
        Assert.Equal("test", dict["p0"]);
    }

    [Fact]
    public void PredicateVisitor_InheritedProperty_Id_Works()
    {
        // Arrange
        var (mapping, dialect) = CreateMapping<DerivedEntity>();
        var visitor = new PredicateVisitor<DerivedEntity>(mapping, dialect);

        // Act - Id is inherited from BaseEntity
        var (sql, parameters) = visitor.Translate(e => e.Id == 5);

        // Assert
        Assert.Equal("(a.[Id] = @p0)", sql);
        var dict = Assert.IsType<Dictionary<string, object>>(parameters);
        Assert.Equal(5, dict["p0"]);
    }

    [Fact]
    public void PredicateVisitor_InheritedProperty_CreatedAt_Works()
    {
        // Arrange
        var (mapping, dialect) = CreateMapping<DerivedEntity>();
        var visitor = new PredicateVisitor<DerivedEntity>(mapping, dialect);
        var testDate = new DateTime(2024, 1, 1);

        // Act - CreatedAt is inherited from BaseEntity
        var (sql, parameters) = visitor.Translate(e => e.CreatedAt >= testDate);

        // Assert
        Assert.Equal("(a.[CreatedAt] >= @p0)", sql);
        var dict = Assert.IsType<Dictionary<string, object>>(parameters);
        Assert.Equal(testDate, dict["p0"]);
    }

    [Fact]
    public void PredicateVisitor_InterfaceProperty_IsActive_Works()
    {
        // Arrange
        var (mapping, dialect) = CreateMapping<DerivedEntity>();
        var visitor = new PredicateVisitor<DerivedEntity>(mapping, dialect);

        // Act - IsActive is from ITrackable interface
        var (sql, _) = visitor.Translate(e => e.IsActive);

        // Assert
        Assert.Equal("a.[IsActive] = 1", sql);
    }

    [Fact]
    public void PredicateVisitor_OwnProperty_Description_Works()
    {
        // Arrange
        var (mapping, dialect) = CreateMapping<DerivedEntity>();
        var visitor = new PredicateVisitor<DerivedEntity>(mapping, dialect);

        // Act - Description is DerivedEntity's own property
        var (sql, parameters) = visitor.Translate(e => e.Description.Contains("test"));

        // Assert
        Assert.Equal("a.[Description] LIKE @p0 ESCAPE '\\'", sql);
        var dict = Assert.IsType<Dictionary<string, object>>(parameters);
        Assert.Equal("%test%", dict["p0"]);
    }

    [Fact]
    public void PredicateVisitor_CombinedInheritedAndOwn_Works()
    {
        // Arrange
        var (mapping, dialect) = CreateMapping<DerivedEntity>();
        var visitor = new PredicateVisitor<DerivedEntity>(mapping, dialect);

        // Act - Combine inherited (Name) with own (Description) and interface (IsActive)
        var (sql, parameters) = visitor.Translate(e =>
            e.Name == "test" && e.Description.Contains("desc") && e.IsActive);

        // Assert
        Assert.Contains("a.[entity_name] = @p0", sql);
        Assert.Contains("a.[Description] LIKE @p1", sql);
        Assert.Contains("a.[IsActive] = 1", sql);
    }

    [Fact]
    public void PredicateVisitor_InheritedStringMethods_Work()
    {
        // Arrange
        var (mapping, dialect) = CreateMapping<DerivedEntity>();
        var visitor = new PredicateVisitor<DerivedEntity>(mapping, dialect);

        // Act - StartsWith on inherited Name property
        var (sql, parameters) = visitor.Translate(e => e.Name.StartsWith("prefix"));

        // Assert
        Assert.Equal("a.[entity_name] LIKE @p0 ESCAPE '\\'", sql);
        var dict = Assert.IsType<Dictionary<string, object>>(parameters);
        Assert.Equal("prefix%", dict["p0"]);
    }

    [Fact]
    public void PredicateVisitor_MultipleInterfaces_Works()
    {
        // Arrange
        var (mapping, dialect) = CreateMapping<MultiInterfaceEntity>();
        var visitor = new PredicateVisitor<MultiInterfaceEntity>(mapping, dialect);

        // Act - Properties from multiple interfaces
        var (sql, parameters) = visitor.Translate(e =>
            e.IsActive && e.CreatedBy == "admin");

        // Assert
        Assert.Contains("a.[IsActive] = 1", sql);
        Assert.Contains("a.[CreatedBy] = @p0", sql);
        var dict = Assert.IsType<Dictionary<string, object>>(parameters);
        Assert.Equal("admin", dict["p0"]);
    }

    #endregion

    #region OrderingVisitor - Inherited Property Tests

    [Fact]
    public void OrderingVisitor_InheritedProperty_Name_Works()
    {
        // Arrange
        var (mapping, dialect) = CreateMapping<DerivedEntity>();
        var visitor = new OrderingVisitor<DerivedEntity>(mapping, dialect);

        // Act - Order by inherited Name property
        var sql = visitor.Translate(e => e.Name);

        // Assert
        Assert.Equal("a.[entity_name]", sql);
    }

    [Fact]
    public void OrderingVisitor_InheritedProperty_Id_Works()
    {
        // Arrange
        var (mapping, dialect) = CreateMapping<DerivedEntity>();
        var visitor = new OrderingVisitor<DerivedEntity>(mapping, dialect);

        // Act - Order by inherited Id property
        var sql = visitor.Translate(e => e.Id, isDescending: true);

        // Assert
        Assert.Equal("a.[Id] DESC", sql);
    }

    [Fact]
    public void OrderingVisitor_InterfaceProperty_IsActive_Works()
    {
        // Arrange
        var (mapping, dialect) = CreateMapping<DerivedEntity>();
        var visitor = new OrderingVisitor<DerivedEntity>(mapping, dialect);

        // Act - Order by interface property
        var sql = visitor.Translate(e => e.IsActive);

        // Assert
        Assert.Equal("a.[IsActive]", sql);
    }

    [Fact]
    public void OrderingVisitor_OwnProperty_Description_Works()
    {
        // Arrange
        var (mapping, dialect) = CreateMapping<DerivedEntity>();
        var visitor = new OrderingVisitor<DerivedEntity>(mapping, dialect);

        // Act - Order by own property
        var sql = visitor.Translate(e => e.Description);

        // Assert
        Assert.Equal("a.[Description]", sql);
    }

    [Fact]
    public void OrderingVisitor_ThenBy_WithInheritedProperties_Works()
    {
        // Arrange
        var (mapping, dialect) = CreateMapping<DerivedEntity>();
        var visitor = new OrderingVisitor<DerivedEntity>(mapping, dialect);

        // Act - Multiple ordering with inherited properties
        visitor.Translate(e => e.Name);
        var sql = visitor.ThenBy(e => e.CreatedAt, isDescending: true);

        // Assert
        Assert.Equal("a.[entity_name], a.[CreatedAt] DESC", sql);
    }

    #endregion

    #region Helper Methods

    private static (EntityMapping Mapping, ISqlDialect Dialect) CreateMapping<TEntity>() where TEntity : class
    {
        var dialect = SqlServerDialect.Instance;
        var builder = new DapperModelBuilder(dialect, dialect.DefaultSchema);
        builder.Entity<TEntity>();
        var mapping = builder.Build()[typeof(TEntity)];
        return (mapping, dialect);
    }

    #endregion
}
