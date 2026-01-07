using Nahmadov.DapperForge.Core.Common;

using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Common;

/// <summary>
/// Tests for LRU (Least Recently Used) cache implementation.
/// </summary>
public class LruCacheTests
{
    [Fact]
    public void LruCache_Should_Store_And_Retrieve_Values()
    {
        // Arrange
        var cache = new LruCache<string, int>(maxSize: 10);

        // Act
        var value1 = cache.GetOrAdd("key1", _ => 100);
        var value2 = cache.GetOrAdd("key2", _ => 200);

        var retrieved1 = cache.GetOrAdd("key1", _ => 999); // Should return cached value

        // Assert
        Assert.Equal(100, value1);
        Assert.Equal(200, value2);
        Assert.Equal(100, retrieved1); // Should not call factory again
    }

    [Fact]
    public void LruCache_Should_Evict_Least_Recently_Used_When_Full()
    {
        // Arrange
        var cache = new LruCache<int, string>(maxSize: 3);
        var factoryCalls = new Dictionary<int, int>();

        string Factory(int key)
        {
            factoryCalls[key] = factoryCalls.GetValueOrDefault(key) + 1;
            return $"value{key}";
        }

        // Act
        cache.GetOrAdd(1, Factory); // Add key 1
        cache.GetOrAdd(2, Factory); // Add key 2
        cache.GetOrAdd(3, Factory); // Add key 3 (cache now full)

        // Access key 1 to make it recently used
        cache.GetOrAdd(1, Factory);

        // Add key 4 - should evict key 2 (LRU)
        cache.GetOrAdd(4, Factory);

        // Try to get key 2 - should need to recompute (was evicted)
        var result = cache.GetOrAdd(2, Factory);

        // Assert
        Assert.Equal(2, factoryCalls[2]); // Key 2 was computed twice (initial + after eviction)
        Assert.Equal(1, factoryCalls[1]); // Key 1 was computed once (never evicted)
        Assert.Equal(1, factoryCalls[3]); // Key 3 was computed once (never evicted)
        Assert.Equal(1, factoryCalls[4]); // Key 4 was computed once
    }

    [Fact]
    public void LruCache_TryGetValue_Should_Return_True_For_Existing_Keys()
    {
        // Arrange
        var cache = new LruCache<string, int>(maxSize: 10);
        cache.GetOrAdd("key1", _ => 100);

        // Act
        var found = cache.TryGetValue("key1", out var value);

        // Assert
        Assert.True(found);
        Assert.Equal(100, value);
    }

    [Fact]
    public void LruCache_TryGetValue_Should_Return_False_For_Missing_Keys()
    {
        // Arrange
        var cache = new LruCache<string, int>(maxSize: 10);

        // Act
        var found = cache.TryGetValue("nonexistent", out var value);

        // Assert
        Assert.False(found);
        Assert.Equal(0, value); // Default value for int
    }

    [Fact]
    public void LruCache_Should_Update_LRU_Order_On_TryGetValue()
    {
        // Arrange
        var cache = new LruCache<int, string>(maxSize: 3);
        cache.GetOrAdd(1, k => $"v{k}");
        cache.GetOrAdd(2, k => $"v{k}");
        cache.GetOrAdd(3, k => $"v{k}");

        // Act
        // Access key 1 via TryGetValue to make it most recently used
        cache.TryGetValue(1, out _);

        // Add key 4 - should evict key 2 (LRU), not key 1
        cache.GetOrAdd(4, k => $"v{k}");

        // Assert
        Assert.True(cache.TryGetValue(1, out _)); // Key 1 should still exist
        Assert.False(cache.TryGetValue(2, out _)); // Key 2 should be evicted
        Assert.True(cache.TryGetValue(3, out _)); // Key 3 should still exist
        Assert.True(cache.TryGetValue(4, out _)); // Key 4 should exist
    }

    [Fact]
    public void LruCache_Should_Track_Count()
    {
        // Arrange
        var cache = new LruCache<int, string>(maxSize: 10);

        // Act & Assert
        Assert.Equal(0, cache.Count);

        cache.GetOrAdd(1, k => $"v{k}");
        Assert.Equal(1, cache.Count);

        cache.GetOrAdd(2, k => $"v{k}");
        Assert.Equal(2, cache.Count);

        cache.GetOrAdd(1, k => $"v{k}"); // Re-accessing doesn't increase count
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void LruCache_Should_Not_Exceed_Max_Size()
    {
        // Arrange
        var cache = new LruCache<int, string>(maxSize: 5);

        // Act
        for (int i = 0; i < 100; i++)
        {
            cache.GetOrAdd(i, k => $"value{k}");
        }

        // Assert
        Assert.Equal(5, cache.Count); // Should never exceed max size
    }

    [Fact]
    public void LruCache_Clear_Should_Remove_All_Entries()
    {
        // Arrange
        var cache = new LruCache<int, string>(maxSize: 10);
        cache.GetOrAdd(1, k => $"v{k}");
        cache.GetOrAdd(2, k => $"v{k}");
        cache.GetOrAdd(3, k => $"v{k}");

        // Act
        cache.Clear();

        // Assert
        Assert.Equal(0, cache.Count);
        Assert.False(cache.TryGetValue(1, out _));
        Assert.False(cache.TryGetValue(2, out _));
        Assert.False(cache.TryGetValue(3, out _));
    }

    [Fact]
    public void LruCache_Should_Handle_Null_Values()
    {
        // Arrange
        var cache = new LruCache<string, string?>(maxSize: 10);

        // Act
        var value = cache.GetOrAdd("key1", _ => null);
        var retrieved = cache.GetOrAdd("key1", _ => "fallback");

        // Assert
        Assert.Null(value);
        Assert.Null(retrieved); // Should return cached null, not fallback
    }

    [Fact]
    public async Task LruCache_Should_Be_Thread_Safe()
    {
        // Arrange
        var cache = new LruCache<int, int>(maxSize: 100);
        var random = new Random(42);
        var threadCount = 10;
        var operationsPerThread = 1000;
        var errors = 0;

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < operationsPerThread; i++)
                {
                    var key = random.Next(0, 50); // Limited key space to test concurrent access
                    cache.GetOrAdd(key, k => k * 2);

                    if (i % 3 == 0)
                    {
                        cache.TryGetValue(key, out _);
                    }
                }
            }
            catch
            {
                Interlocked.Increment(ref errors);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(0, errors); // No exceptions should occur
        Assert.True(cache.Count <= 100); // Should not exceed max size
    }

    [Fact]
    public void LruCache_Should_Call_Factory_Only_Once_Per_Key()
    {
        // Arrange
        var cache = new LruCache<string, int>(maxSize: 10);
        var callCount = 0;

        int ExpensiveFactory(string key)
        {
            Interlocked.Increment(ref callCount);
            return key.Length;
        }

        // Act
        var result1 = cache.GetOrAdd("test", ExpensiveFactory);
        var result2 = cache.GetOrAdd("test", ExpensiveFactory);
        var result3 = cache.GetOrAdd("test", ExpensiveFactory);

        // Assert
        Assert.Equal(1, callCount); // Factory should be called only once
        Assert.Equal(4, result1);
        Assert.Equal(4, result2);
        Assert.Equal(4, result3);
    }

    [Fact]
    public void LruCache_Should_Throw_When_MaxSize_Is_Zero_Or_Negative()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new LruCache<int, int>(maxSize: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LruCache<int, int>(maxSize: -1));
    }

    [Fact]
    public void LruCache_Should_Maintain_LRU_Order_Across_Multiple_Operations()
    {
        // Arrange
        var cache = new LruCache<int, string>(maxSize: 3);
        var factoryCallCount = new Dictionary<int, int>();

        string Factory(int key)
        {
            factoryCallCount[key] = factoryCallCount.GetValueOrDefault(key) + 1;
            return $"value{key}";
        }

        // Act
        cache.GetOrAdd(1, Factory); // LRU order: [1]
        cache.GetOrAdd(2, Factory); // LRU order: [2, 1]
        cache.GetOrAdd(3, Factory); // LRU order: [3, 2, 1]

        cache.GetOrAdd(2, Factory); // LRU order: [2, 3, 1] - 2 moved to front
        cache.GetOrAdd(4, Factory); // LRU order: [4, 2, 3] - evicts 1

        // Assert
        Assert.True(cache.TryGetValue(2, out _)); // Should exist
        Assert.True(cache.TryGetValue(3, out _)); // Should exist
        Assert.True(cache.TryGetValue(4, out _)); // Should exist
        Assert.False(cache.TryGetValue(1, out _)); // Should be evicted

        Assert.Equal(1, factoryCallCount[1]); // Called once, then evicted
        Assert.Equal(1, factoryCallCount[2]); // Called once, accessed multiple times
        Assert.Equal(1, factoryCallCount[3]); // Called once
        Assert.Equal(1, factoryCallCount[4]); // Called once
    }
}
