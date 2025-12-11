using System.Linq.Expressions;

using Xunit;

using Nahmadov.DapperForge.Core.Builders;
using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.Core.Mapping;
using Nahmadov.DapperForge.SqlServer;

namespace Nahmadov.DapperForge.UnitTests.Context;

/// <summary>
/// Unit tests for DapperQueryable fluent query builder.
/// </summary>
public sealed class DapperQueryableTests
{
    private static DapperQueryable<TestEntity> CreateQueryable()
    {
        var mapping = EntityMappingCache<TestEntity>.Mapping;
        var generator = new SqlGenerator<TestEntity>(SqlServerDialect.Instance, mapping);
        
        // Mock context - just for testing DapperQueryable behavior
        var context = new MockDapperDbContext();
        return new DapperQueryable<TestEntity>(context, generator, mapping);
    }

    [Fact]
    public void Where_WithValidPredicate_ReturnsQueryable()
    {
        // Arrange
        var queryable = CreateQueryable();

        // Act
        var result = queryable.Where(c => c.IsActive);

        // Assert
        Assert.NotNull(result);
        Assert.Same(queryable, result);
    }

    [Fact]
    public void OrderBy_WithValidExpression_ReturnsQueryable()
    {
        // Arrange
        var queryable = CreateQueryable();

        // Act
        var result = queryable.OrderBy(c => (object)c.Name);

        // Assert
        Assert.NotNull(result);
        Assert.Same(queryable, result);
    }

    [Fact]
    public void OrderByDescending_WithValidExpression_ReturnsQueryable()
    {
        // Arrange
        var queryable = CreateQueryable();

        // Act
        var result = queryable.OrderByDescending(c => (object)c.CreatedDate);

        // Assert
        Assert.NotNull(result);
        Assert.Same(queryable, result);
    }

    [Fact]
    public void Skip_WithValidCount_ReturnsQueryable()
    {
        // Arrange
        var queryable = CreateQueryable();

        // Act
        var result = queryable.Skip(10);

        // Assert
        Assert.NotNull(result);
        Assert.Same(queryable, result);
    }

    [Fact]
    public void Skip_WithZeroCount_ReturnsQueryable()
    {
        // Arrange
        var queryable = CreateQueryable();

        // Act
        var result = queryable.Skip(0);

        // Assert
        Assert.NotNull(result);
        Assert.Same(queryable, result);
    }

    [Fact]
    public void Skip_WithNegativeCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var queryable = CreateQueryable();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => queryable.Skip(-1));
    }

    [Fact]
    public void Take_WithValidCount_ReturnsQueryable()
    {
        // Arrange
        var queryable = CreateQueryable();

        // Act
        var result = queryable.Take(10);

        // Assert
        Assert.NotNull(result);
        Assert.Same(queryable, result);
    }

    [Fact]
    public void Take_WithZeroCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var queryable = CreateQueryable();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => queryable.Take(0));
    }

    [Fact]
    public void Take_WithNegativeCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var queryable = CreateQueryable();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => queryable.Take(-1));
    }

    [Fact]
    public void Chaining_Where_OrderBy()
    {
        // Arrange & Act
        var queryable = CreateQueryable()
            .Where(c => c.IsActive)
            .OrderBy(c => (object)c.Name);

        // Assert
        Assert.NotNull(queryable);
    }

    [Fact]
    public void Chaining_OrderBy_Skip_Take()
    {
        // Arrange & Act
        var queryable = CreateQueryable()
            .OrderBy(c => (object)c.Id)
            .Skip(0)
            .Take(10);

        // Assert
        Assert.NotNull(queryable);
    }

    [Fact]
    public void Chaining_ComplexQuery()
    {
        // Arrange & Act
        var queryable = CreateQueryable()
            .Where(c => c.IsActive)
            .OrderByDescending(c => (object)c.CreatedDate)
            .Skip(0)
            .Take(25);

        // Assert
        Assert.NotNull(queryable);
    }

    [Fact]
    public void Fluent_Api_Maintains_Chainability()
    {
        // Arrange & Act
        var queryable = CreateQueryable()
            .Where(x => x.IsActive)
            .OrderBy(x => (object)x.Name)
            .Skip(10)
            .Take(20);

        // Assert
        Assert.NotNull(queryable);
    }

    [Fact]
    public void ToListAsync_MethodExists()
    {
        // Arrange
        var queryable = CreateQueryable();

        // Act
        var method = queryable.GetType().GetMethod("ToListAsync");

        // Assert
        Assert.NotNull(method);
    }

    [Fact]
    public void FirstOrDefaultAsync_MethodExists()
    {
        // Arrange
        var queryable = CreateQueryable();

        // Act
        var method = queryable.GetType().GetMethod("FirstOrDefaultAsync");

        // Assert
        Assert.NotNull(method);
    }

    [Fact]
    public void SingleOrDefaultAsync_MethodExists()
    {
        // Arrange
        var queryable = CreateQueryable();

        // Act
        var method = queryable.GetType().GetMethod("SingleOrDefaultAsync");

        // Assert
        Assert.NotNull(method);
    }

    [Fact]
    public void CountAsync_MethodExists()
    {
        // Arrange
        var queryable = CreateQueryable();

        // Act
        var method = queryable.GetType().GetMethod("CountAsync");

        // Assert
        Assert.NotNull(method);
    }

    // Test entity
    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    // Mock context for testing
    private class MockDapperDbContext : DapperDbContext
    {
        public MockDapperDbContext() 
            : base(new Nahmadov.DapperForge.Core.Common.DapperDbContextOptions<MockDapperDbContext>
            {
                ConnectionFactory = () => new Fakes.FakeDbConnection(),
                Dialect = SqlServerDialect.Instance
            })
        {
        }
    }
}
