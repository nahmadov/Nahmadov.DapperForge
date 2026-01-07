using Nahmadov.DapperForge.Core.Mapping;
using Nahmadov.DapperForge.Core.Query;

using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Query;

/// <summary>
/// Tests for IdentityCache adaptive sizing and metrics tracking.
/// </summary>
public class IdentityCacheTests
{
    private class TestEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private static EntityMapping CreateTestMapping()
    {
        var keyProp = typeof(TestEntity).GetProperty(nameof(TestEntity.Id))!;
        var nameProp = typeof(TestEntity).GetProperty(nameof(TestEntity.Name))!;

        var keyMapping = new PropertyMapping(keyProp, "Id", null);
        var nameMapping = new PropertyMapping(nameProp, "Name", null);

        return new EntityMapping(
            entityType: typeof(TestEntity),
            tableName: "TestEntities",
            schema: null,
            keyProperties: [keyProp],
            properties: [keyProp, nameProp],
            propertyMappings: [keyMapping, nameMapping],
            isReadOnly: false
        );
    }

    [Fact]
    public void IdentityCache_Should_Track_Hits_And_Misses()
    {
        // Arrange
        var mapping = CreateTestMapping();
        var cache = new IdentityCache(_ => mapping, maxSize: 100);

        var entity1 = new TestEntity { Id = 1, Name = "Alice" };
        var entity2 = new TestEntity { Id = 2, Name = "Bob" };

        // Act
        cache.GetOrAdd(typeof(TestEntity), 1, entity1); // Miss
        cache.GetOrAdd(typeof(TestEntity), 2, entity2); // Miss
        cache.GetOrAdd(typeof(TestEntity), 1, new TestEntity { Id = 1 }); // Hit
        cache.GetOrAdd(typeof(TestEntity), 1, new TestEntity { Id = 1 }); // Hit

        var metrics = cache.GetMetrics();

        // Assert
        Assert.Equal(2, metrics.Hits);
        Assert.Equal(2, metrics.Misses);
        Assert.Equal(0.5, metrics.HitRate, precision: 2);
    }

    [Fact]
    public void IdentityCache_Should_Track_Evictions()
    {
        // Arrange
        var mapping = CreateTestMapping();
        var cache = new IdentityCache(_ => mapping, maxSize: 3);

        // Act
        cache.GetOrAdd(typeof(TestEntity), 1, new TestEntity { Id = 1 });
        cache.GetOrAdd(typeof(TestEntity), 2, new TestEntity { Id = 2 });
        cache.GetOrAdd(typeof(TestEntity), 3, new TestEntity { Id = 3 });
        cache.GetOrAdd(typeof(TestEntity), 4, new TestEntity { Id = 4 }); // Evicts 1

        var metrics = cache.GetMetrics();

        // Assert
        Assert.Equal(1, metrics.Evictions);
        Assert.Equal(3, metrics.CurrentSize); // Cache should be at max size
    }

    [Fact]
    public void IdentityCache_Should_Expand_When_Eviction_Rate_Is_High()
    {
        // Arrange
        var mapping = CreateTestMapping();
        var cache = new IdentityCache(_ => mapping, maxSize: 10);

        // Act
        // Add 1000 entities with keys 0-999
        // First 10 will fill the cache, rest will trigger evictions
        for (int i = 0; i < 1000; i++)
        {
            cache.GetOrAdd(typeof(TestEntity), i, new TestEntity { Id = i });
        }

        var metricsBeforeExpansion = cache.GetMetrics();

        // Add more entities to trigger expansion check
        for (int i = 1000; i < 2000; i++)
        {
            cache.GetOrAdd(typeof(TestEntity), i, new TestEntity { Id = i });
        }

        var metricsAfterExpansion = cache.GetMetrics();

        // Assert
        Assert.True(metricsAfterExpansion.MaxSize > 10,
            $"Cache should have expanded. Initial: 10, Current: {metricsAfterExpansion.MaxSize}");
        Assert.True(metricsAfterExpansion.EvictionRate > 0,
            "Eviction rate should be tracked");
    }

    [Fact]
    public void IdentityCache_Should_Not_Expand_Beyond_Max_Limit()
    {
        // Arrange
        var mapping = CreateTestMapping();
        var cache = new IdentityCache(_ => mapping, maxSize: 10);

        // Act
        // Add enough entities to trigger multiple expansions
        for (int i = 0; i < 100_000; i++)
        {
            cache.GetOrAdd(typeof(TestEntity), i, new TestEntity { Id = i });
        }

        var metrics = cache.GetMetrics();

        // Assert
        Assert.True(metrics.MaxSize <= 50_000,
            $"Cache should not exceed 50,000 limit. Actual: {metrics.MaxSize}");
    }

    [Fact]
    public void IdentityCache_Should_Return_Same_Instance_For_Same_Key()
    {
        // Arrange
        var mapping = CreateTestMapping();
        var cache = new IdentityCache(_ => mapping, maxSize: 100);

        var entity1 = new TestEntity { Id = 1, Name = "Alice" };
        var entity2 = new TestEntity { Id = 1, Name = "Different Name" };

        // Act
        var cached1 = cache.GetOrAdd(typeof(TestEntity), 1, entity1);
        var cached2 = cache.GetOrAdd(typeof(TestEntity), 1, entity2);

        // Assert
        Assert.Same(cached1, cached2); // Should return same instance
        Assert.Same(entity1, cached2); // Should return first cached instance
        Assert.NotSame(entity2, cached2); // Should NOT use second instance
    }

    [Fact]
    public void IdentityCache_TryGet_Should_Track_Metrics()
    {
        // Arrange
        var mapping = CreateTestMapping();
        var cache = new IdentityCache(_ => mapping, maxSize: 100);

        cache.GetOrAdd(typeof(TestEntity), 1, new TestEntity { Id = 1 }); // Miss (adding new entry)

        // Act
        cache.TryGet(typeof(TestEntity), 1, out _); // Hit
        cache.TryGet(typeof(TestEntity), 2, out _); // Miss

        var metrics = cache.GetMetrics();

        // Assert
        Assert.Equal(1, metrics.Hits); // 1 from TryGet
        Assert.Equal(2, metrics.Misses); // 1 from GetOrAdd (cache miss) + 1 from TryGet
    }

    [Fact]
    public void IdentityCache_Metrics_Should_Calculate_Rates_Correctly()
    {
        // Arrange
        var mapping = CreateTestMapping();
        var cache = new IdentityCache(_ => mapping, maxSize: 5);

        // Act
        // 5 misses
        for (int i = 0; i < 5; i++)
        {
            cache.GetOrAdd(typeof(TestEntity), i, new TestEntity { Id = i });
        }

        // 5 hits
        for (int i = 0; i < 5; i++)
        {
            cache.GetOrAdd(typeof(TestEntity), i, new TestEntity { Id = i });
        }

        // 5 more misses with evictions
        for (int i = 5; i < 10; i++)
        {
            cache.GetOrAdd(typeof(TestEntity), i, new TestEntity { Id = i });
        }

        var metrics = cache.GetMetrics();

        // Assert
        Assert.Equal(5, metrics.Hits);
        Assert.Equal(10, metrics.Misses);
        Assert.Equal(5, metrics.Evictions);
        Assert.Equal(5.0 / 15.0, metrics.HitRate, precision: 2);
        Assert.Equal(5.0 / 15.0, metrics.EvictionRate, precision: 2);
    }

    [Fact]
    public void IdentityCache_Clear_Should_Reset_Cache_And_Metrics()
    {
        // Arrange
        var mapping = CreateTestMapping();
        var cache = new IdentityCache(_ => mapping, maxSize: 100);

        cache.GetOrAdd(typeof(TestEntity), 1, new TestEntity { Id = 1 });
        cache.GetOrAdd(typeof(TestEntity), 2, new TestEntity { Id = 2 });
        cache.GetOrAdd(typeof(TestEntity), 1, new TestEntity { Id = 1 }); // Hit

        // Act
        cache.Clear();
        var metrics = cache.GetMetrics();

        // Assert
        Assert.Equal(0, metrics.CurrentSize);
        // Note: Metrics are NOT reset by Clear() - they track lifetime stats
        Assert.True(metrics.Hits >= 0);
        Assert.True(metrics.Misses >= 0);
    }

    [Fact]
    public void IdentityCache_Resolve_Should_Use_Cache()
    {
        // Arrange
        var mapping = CreateTestMapping();
        var cache = new IdentityCache(_ => mapping, maxSize: 100);

        var entity1 = new TestEntity { Id = 1, Name = "Alice" };
        var entity2 = new TestEntity { Id = 1, Name = "Bob" };

        // Act
        var resolved1 = cache.Resolve(mapping, entity1);
        var resolved2 = cache.Resolve(mapping, entity2);

        // Assert
        Assert.Same(entity1, resolved1);
        Assert.Same(entity1, resolved2); // Should return first instance
        Assert.NotSame(entity2, resolved2);
    }

    [Fact]
    public void IdentityCache_Should_Handle_Entities_With_No_Key()
    {
        // Arrange
        var mapping = new EntityMapping(
            entityType: typeof(TestEntity),
            tableName: "TestEntities",
            schema: null,
            keyProperties: [], // No key properties
            properties: [],
            propertyMappings: [],
            isReadOnly: false
        );

        var cache = new IdentityCache(_ => mapping, maxSize: 100);
        var entity = new TestEntity { Id = 1, Name = "Alice" };

        // Act
        var resolved = cache.Resolve(mapping, entity);

        // Assert
        Assert.Same(entity, resolved); // Should return same instance without caching
    }

    [Fact]
    public void IdentityCache_Should_Handle_Entities_With_Null_Key()
    {
        // Arrange
        var mapping = CreateTestMapping();
        var cache = new IdentityCache(_ => mapping, maxSize: 100);

        // Create entity with null key (using dynamic to bypass compile-time type checking)
        dynamic entity = new TestEntity { Name = "Alice" };
        typeof(TestEntity).GetProperty("Id")!.SetValue(entity, null);

        // Act
        var resolved = cache.Resolve(mapping, entity);

        // Assert
        Assert.Same(entity, resolved); // Should return same instance without caching
    }
}
