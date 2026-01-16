using System.Linq.Expressions;

using Xunit;

using Nahmadov.DapperForge.Core.Querying.Predicates;

namespace Nahmadov.DapperForge.UnitTests.Builders;

/// <summary>
/// Tests for expression structural hashing and equality comparison.
/// Verifies that the improved cache key strategy correctly identifies
/// structurally equivalent expressions.
/// </summary>
public sealed class ExpressionCacheKeyTests
{
    private class TestEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    [Fact]
    public void StructuralHasher_IdenticalExpressions_ProduceSameHash()
    {
        // Arrange
        Expression<Func<TestEntity, bool>> expr1 = x => x.Id == 42;
        Expression<Func<TestEntity, bool>> expr2 = x => x.Id == 42;

        // Act
        var hash1 = ExpressionStructuralHasher.ComputeHash(expr1);
        var hash2 = ExpressionStructuralHasher.ComputeHash(expr2);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void StructuralHasher_EquivalentExpressionsWithDifferentParameterNames_ProduceSameHash()
    {
        // Arrange
        Expression<Func<TestEntity, bool>> expr1 = x => x.Id == 42;
        Expression<Func<TestEntity, bool>> expr2 = entity => entity.Id == 42;

        // Act
        var hash1 = ExpressionStructuralHasher.ComputeHash(expr1);
        var hash2 = ExpressionStructuralHasher.ComputeHash(expr2);

        // Assert - Parameter names should not affect structural hash
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void StructuralHasher_DifferentExpressions_ProduceDifferentHashes()
    {
        // Arrange
        Expression<Func<TestEntity, bool>> expr1 = x => x.Id == 42;
        Expression<Func<TestEntity, bool>> expr2 = x => x.Id == 100;

        // Act
        var hash1 = ExpressionStructuralHasher.ComputeHash(expr1);
        var hash2 = ExpressionStructuralHasher.ComputeHash(expr2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void StructuralHasher_ComplexExpressions_ProduceSameHashForEquivalent()
    {
        // Arrange
        Expression<Func<TestEntity, bool>> expr1 = x => x.Id > 10 && x.Name!.StartsWith("Test") && x.IsActive;
        Expression<Func<TestEntity, bool>> expr2 = entity => entity.Id > 10 && entity.Name!.StartsWith("Test") && entity.IsActive;

        // Act
        var hash1 = ExpressionStructuralHasher.ComputeHash(expr1);
        var hash2 = ExpressionStructuralHasher.ComputeHash(expr2);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void StructuralEqualityComparer_IdenticalExpressions_ReturnsTrue()
    {
        // Arrange
        Expression<Func<TestEntity, bool>> expr1 = x => x.Id == 42;
        Expression<Func<TestEntity, bool>> expr2 = x => x.Id == 42;

        // Act
        var areEqual = ExpressionStructuralEqualityComparer.AreEqual(expr1, expr2);

        // Assert
        Assert.True(areEqual);
    }

    [Fact]
    public void StructuralEqualityComparer_EquivalentExpressionsWithDifferentParameterNames_ReturnsTrue()
    {
        // Arrange
        Expression<Func<TestEntity, bool>> expr1 = x => x.Id == 42;
        Expression<Func<TestEntity, bool>> expr2 = entity => entity.Id == 42;

        // Act
        var areEqual = ExpressionStructuralEqualityComparer.AreEqual(expr1, expr2);

        // Assert - Should be structurally equal despite different parameter names
        Assert.True(areEqual);
    }

    [Fact]
    public void StructuralEqualityComparer_DifferentExpressions_ReturnsFalse()
    {
        // Arrange
        Expression<Func<TestEntity, bool>> expr1 = x => x.Id == 42;
        Expression<Func<TestEntity, bool>> expr2 = x => x.Id == 100;

        // Act
        var areEqual = ExpressionStructuralEqualityComparer.AreEqual(expr1, expr2);

        // Assert
        Assert.False(areEqual);
    }

    [Fact]
    public void StructuralEqualityComparer_ComplexExpressions_CorrectlyCompares()
    {
        // Arrange
        Expression<Func<TestEntity, bool>> expr1 = x => x.Id > 10 && x.Name!.Contains("Test");
        Expression<Func<TestEntity, bool>> expr2 = y => y.Id > 10 && y.Name!.Contains("Test");

        // Act
        var areEqual = ExpressionStructuralEqualityComparer.AreEqual(expr1, expr2);

        // Assert
        Assert.True(areEqual);
    }

    [Fact]
    public void StructuralEqualityComparer_NullExpressions_ReturnsTrue()
    {
        // Act
        var areEqual = ExpressionStructuralEqualityComparer.AreEqual(null, null);

        // Assert
        Assert.True(areEqual);
    }

    [Fact]
    public void StructuralEqualityComparer_OneNullExpression_ReturnsFalse()
    {
        // Arrange
        Expression<Func<TestEntity, bool>> expr = x => x.Id == 42;

        // Act
        var areEqual1 = ExpressionStructuralEqualityComparer.AreEqual(expr, null);
        var areEqual2 = ExpressionStructuralEqualityComparer.AreEqual(null, expr);

        // Assert
        Assert.False(areEqual1);
        Assert.False(areEqual2);
    }

    [Fact]
    public void StructuralHasher_StringMethods_ProducesConsistentHash()
    {
        // Arrange
        Expression<Func<TestEntity, bool>> expr1 = x => x.Name!.StartsWith("Test");
        Expression<Func<TestEntity, bool>> expr2 = entity => entity.Name!.StartsWith("Test");

        // Act
        var hash1 = ExpressionStructuralHasher.ComputeHash(expr1);
        var hash2 = ExpressionStructuralHasher.ComputeHash(expr2);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void StructuralHasher_BooleanExpressions_ProducesConsistentHash()
    {
        // Arrange
        Expression<Func<TestEntity, bool>> expr1 = x => x.IsActive;
        Expression<Func<TestEntity, bool>> expr2 = y => y.IsActive;

        // Act
        var hash1 = ExpressionStructuralHasher.ComputeHash(expr1);
        var hash2 = ExpressionStructuralHasher.ComputeHash(expr2);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void StructuralHasher_NegatedBooleanExpressions_ProducesDifferentHash()
    {
        // Arrange
        Expression<Func<TestEntity, bool>> expr1 = x => x.IsActive;
        Expression<Func<TestEntity, bool>> expr2 = x => !x.IsActive;

        // Act
        var hash1 = ExpressionStructuralHasher.ComputeHash(expr1);
        var hash2 = ExpressionStructuralHasher.ComputeHash(expr2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void StructuralHasher_OrVsAnd_ProducesDifferentHash()
    {
        // Arrange
        Expression<Func<TestEntity, bool>> expr1 = x => x.Id > 10 && x.IsActive;
        Expression<Func<TestEntity, bool>> expr2 = x => x.Id > 10 || x.IsActive;

        // Act
        var hash1 = ExpressionStructuralHasher.ComputeHash(expr1);
        var hash2 = ExpressionStructuralHasher.ComputeHash(expr2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void StructuralEqualityComparer_MethodCallsWithDifferentArguments_ReturnsFalse()
    {
        // Arrange
        Expression<Func<TestEntity, bool>> expr1 = x => x.Name!.Contains("Test");
        Expression<Func<TestEntity, bool>> expr2 = x => x.Name!.Contains("Other");

        // Act
        var areEqual = ExpressionStructuralEqualityComparer.AreEqual(expr1, expr2);

        // Assert
        Assert.False(areEqual);
    }

    [Fact]
    public void StructuralHasher_NullComparisons_ProducesConsistentHash()
    {
        // Arrange
        Expression<Func<TestEntity, bool>> expr1 = x => x.Name == null;
        Expression<Func<TestEntity, bool>> expr2 = entity => entity.Name == null;

        // Act
        var hash1 = ExpressionStructuralHasher.ComputeHash(expr1);
        var hash2 = ExpressionStructuralHasher.ComputeHash(expr2);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void StructuralHasher_DateTimeComparisons_ProducesConsistentHash()
    {
        // Arrange
        var testDate = new DateTime(2024, 1, 1);
        Expression<Func<TestEntity, bool>> expr1 = x => x.CreatedDate > testDate;
        Expression<Func<TestEntity, bool>> expr2 = y => y.CreatedDate > testDate;

        // Act
        var hash1 = ExpressionStructuralHasher.ComputeHash(expr1);
        var hash2 = ExpressionStructuralHasher.ComputeHash(expr2);

        // Assert
        Assert.Equal(hash1, hash2);
    }
}

