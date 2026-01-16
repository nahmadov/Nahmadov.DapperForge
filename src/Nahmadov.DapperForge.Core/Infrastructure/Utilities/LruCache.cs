using System.Diagnostics.CodeAnalysis;

namespace Nahmadov.DapperForge.Core.Infrastructure.Utilities;
/// <summary>
/// Thread-safe LRU (Least Recently Used) cache with bounded size.
/// </summary>
/// <typeparam name="TKey">Cache key type.</typeparam>
/// <typeparam name="TValue">Cache value type.</typeparam>
/// <remarks>
/// <para><b>Thread Safety:</b></para>
/// <para>
/// All operations are thread-safe using a single lock for simplicity.
/// While this may seem less performant than lock-free approaches, for typical cache sizes (1000-10000 entries)
/// and cache hit rates (>90%), the lock overhead is negligible compared to the cost of expression compilation.
/// </para>
/// <para><b>Performance Characteristics:</b></para>
/// <list type="bullet">
/// <item>
/// <b>Lookup Performance:</b> O(1) average case for both TryGetValue and GetOrAdd operations.
/// </item>
/// <item>
/// <b>LRU Update:</b> O(1) using LinkedList for tracking access order.
/// </item>
/// <item>
/// <b>Memory Usage:</b> Bounded by maxSize. Each cached entry stores the value reference and a LinkedListNode.
/// Approximate memory per entry: value reference (8 bytes) + node (24 bytes) + dictionary overhead (~32 bytes).
/// </item>
/// <item>
/// <b>Eviction:</b> When cache is full, only the least recently used entry is evicted (no cache thrashing).
/// </item>
/// </list>
/// <para><b>Design Rationale:</b></para>
/// <para>
/// This cache is designed specifically for expression compilation caching where:
/// - Cache misses are very expensive (expression compilation costs milliseconds)
/// - Cache hits should be ultra-fast (microseconds)
/// - Memory bounds prevent unbounded growth
/// - LRU eviction ensures frequently-used expressions stay cached
/// - Thread safety is critical since the cache is shared across all queries
/// </para>
/// </remarks>
internal sealed class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly Dictionary<TKey, CacheEntry> _cache;
    private readonly LinkedList<TKey> _lruList = new();
    private readonly int _maxSize;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new LRU cache with the specified maximum size.
    /// </summary>
    /// <param name="maxSize">Maximum number of entries to cache. Must be greater than 0.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when maxSize is less than or equal to 0.</exception>
    public LruCache(int maxSize)
    {
        if (maxSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSize), "Cache size must be greater than 0");

        _maxSize = maxSize;
        _cache = new Dictionary<TKey, CacheEntry>(maxSize);
    }

    /// <summary>
    /// Gets the current number of items in the cache.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _cache.Count;
            }
        }
    }

    /// <summary>
    /// Attempts to get a value from the cache.
    /// If found, updates the entry's position in the LRU list (marks as recently used).
    /// </summary>
    /// <param name="key">The key to lookup.</param>
    /// <param name="value">The cached value if found; otherwise default.</param>
    /// <returns>True if the key was found in the cache; otherwise false.</returns>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                // Move to front of LRU list (most recently used)
                _lruList.Remove(entry.Node);
                _lruList.AddFirst(entry.Node);

                value = entry.Value;
                return true;
            }

            value = default;
            return false;
        }
    }

    /// <summary>
    /// Gets or adds a value to the cache using the provided factory function.
    /// If the key exists, returns the cached value and updates LRU order.
    /// If the key doesn't exist, invokes the factory to create the value, adds it to the cache, and returns it.
    /// When the cache is full, evicts the least recently used entry before adding the new one.
    /// </summary>
    /// <param name="key">The key to lookup or add.</param>
    /// <param name="valueFactory">Function to create the value if not found in cache.</param>
    /// <returns>The cached or newly created value.</returns>
    /// <remarks>
    /// <b>Thread Safety:</b> The valueFactory is called while holding the lock to ensure consistency.
    /// If the factory is expensive, consider using TryGetValue first to check for cache hits without the factory.
    /// However, for expression compilation, the cache hit rate is typically >95%, so this approach is fine.
    /// </remarks>
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
    {
        lock (_lock)
        {
            // Fast path: value already cached
            if (_cache.TryGetValue(key, out var existing))
            {
                // Move to front of LRU list (most recently used)
                _lruList.Remove(existing.Node);
                _lruList.AddFirst(existing.Node);
                return existing.Value;
            }

            // Evict LRU entry if cache is full
            if (_cache.Count >= _maxSize)
            {
                var lruKey = _lruList.Last!.Value;
                _cache.Remove(lruKey);
                _lruList.RemoveLast();
            }

            // Add new entry
            var value = valueFactory(key);
            var node = _lruList.AddFirst(key);
            _cache[key] = new CacheEntry(value, node);
            return value;
        }
    }

    /// <summary>
    /// Clears all entries from the cache.
    /// </summary>
    /// <remarks>
    /// This should rarely be needed in practice. LRU eviction handles cache size automatically.
    /// </remarks>
    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _lruList.Clear();
        }
    }

    /// <summary>
    /// Cache entry containing value and LRU list node.
    /// </summary>
    private readonly record struct CacheEntry(TValue Value, LinkedListNode<TKey> Node);
}

