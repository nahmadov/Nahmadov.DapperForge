using System.Reflection;
using System.Runtime.CompilerServices;

using Nahmadov.DapperForge.Core.Mapping;

namespace Nahmadov.DapperForge.Core.Query;

/// <summary>
/// Manages identity resolution for Include operations.
/// Ensures the same entity key returns the same instance (object identity preservation).
/// Uses LRU (Least Recently Used) eviction to prevent unbounded memory growth.
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b></para>
/// <para>
/// When executing Include queries, the same entity may appear multiple times in the result set
/// (e.g., same Customer for multiple Orders). The identity cache ensures that all references
/// to the same entity point to the same object instance, preventing duplicate objects in memory.
/// </para>
/// <para><b>Performance Characteristics:</b></para>
/// <list type="bullet">
/// <item>
/// <b>Cache Size:</b> Default maximum is 10,000 entities per query. Configurable via constructor parameter.
/// </item>
/// <item>
/// <b>LRU Eviction:</b> When cache is full, the least recently accessed entity is evicted.
/// Uses LinkedList for O(1) LRU updates and Dictionary for O(1) lookups.
/// </item>
/// <item>
/// <b>Lookup Performance:</b> O(1) average case for both TryGet and GetOrAdd operations.
/// </item>
/// <item>
/// <b>Memory Usage:</b> Bounded by maxSize. Each cached entity stores the instance reference and a LinkedListNode.
/// Approximate memory per entry: instance reference (8 bytes) + node (24 bytes) + dictionary overhead (~32 bytes).
/// </item>
/// <item>
/// <b>Thread Safety:</b> NOT thread-safe. Intended for single query execution scope.
/// Each query gets its own IdentityCache instance.
/// </item>
/// </list>
/// <para><b>Example:</b></para>
/// <code>
/// // Without identity cache: 3 separate Customer instances (wasteful)
/// var orders = [
///     { Id = 1, CustomerId = 100, Customer = new Customer { Id = 100 } },
///     { Id = 2, CustomerId = 100, Customer = new Customer { Id = 100 } },
///     { Id = 3, CustomerId = 100, Customer = new Customer { Id = 100 } }
/// ];
///
/// // With identity cache: 3 orders share the same Customer instance
/// orders[0].Customer == orders[1].Customer == orders[2].Customer // true (reference equality)
/// </code>
/// </remarks>
internal sealed class IdentityCache(Func<Type, EntityMapping> resolveMapping, int maxSize = 10_000)
{
    private readonly Dictionary<(Type type, object key), CacheEntry> _cache = [];
    private readonly LinkedList<(Type type, object key)> _lruList = new();
    private readonly Func<Type, EntityMapping> _resolveMapping = resolveMapping;
    private readonly int _maxSize = maxSize;

    /// <summary>
    /// Resolves an entity instance, returning cached instance if exists.
    /// </summary>
    public object Resolve(EntityMapping mapping, object instance)
    {
        var keyProp = mapping.KeyProperties.FirstOrDefault();
        if (keyProp is null)
            return instance;

        var key = keyProp.GetValue(instance);
        if (key is null)
            return instance;

        return GetOrAdd(mapping.EntityType, key, instance);
    }

    /// <summary>
    /// Resolves an entity instance by type.
    /// </summary>
    public object Resolve(Type entityType, object instance)
    {
        var mapping = _resolveMapping(entityType);
        return Resolve(mapping, instance);
    }

    /// <summary>
    /// Gets or adds an instance to the cache with LRU eviction.
    /// If instance exists, updates its access time and returns cached instance.
    /// If cache is full, evicts the least recently used entry before adding new instance.
    /// </summary>
    /// <param name="type">Entity type (used as part of cache key).</param>
    /// <param name="key">Entity key value (primary key).</param>
    /// <param name="instance">New instance to add if not cached.</param>
    /// <returns>Cached instance if exists, otherwise the provided instance (now cached).</returns>
    /// <remarks>
    /// <b>Performance:</b> O(1) average case.
    /// Cache hit: Updates LRU list (2 linked list operations) and returns cached instance.
    /// Cache miss: Adds to dictionary and LRU list. If full, removes LRU entry first (3-4 operations total).
    /// </remarks>
    public object GetOrAdd(Type type, object key, object instance)
    {
        var cacheKey = (type, key);
        if (_cache.TryGetValue(cacheKey, out var existing))
        {
            _lruList.Remove(existing.Node);
            _lruList.AddFirst(existing.Node);
            return existing.Instance;
        }

        if (_cache.Count >= _maxSize)
        {
            var lruKey = _lruList.Last!.Value;
            _cache.Remove(lruKey);
            _lruList.RemoveLast();
        }

        var node = _lruList.AddFirst(cacheKey);
        _cache[cacheKey] = new CacheEntry(instance, node);
        return instance;
    }

    /// <summary>
    /// Tries to get an existing instance from the cache and updates LRU order.
    /// </summary>
    public bool TryGet(Type type, object key, out object? instance)
    {
        var cacheKey = (type, key);
        if (_cache.TryGetValue(cacheKey, out var entry))
        {
            _lruList.Remove(entry.Node);
            _lruList.AddFirst(entry.Node);
            instance = entry.Instance;
            return true;
        }

        instance = null;
        return false;
    }

    /// <summary>
    /// Clears the identity cache.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        _lruList.Clear();
    }

    /// <summary>
    /// Builds an index of entities by their key property.
    /// </summary>
    public static Dictionary<object, object> BuildKeyIndex(
        EntityMapping mapping,
        IEnumerable<object> entities)
    {
        var dict = new Dictionary<object, object>();

        var keyProp = mapping.KeyProperties.FirstOrDefault();
        if (keyProp is null)
            return dict;

        foreach (var entity in entities)
        {
            var key = keyProp.GetValue(entity);
            if (key is not null)
                dict[key] = entity;
        }

        return dict;
    }

    /// <summary>
    /// Groups entities by their foreign key property value.
    /// </summary>
    public static Dictionary<object, List<object>> GroupByForeignKey(
        PropertyInfo fkProperty,
        IEnumerable<object> entities)
    {
        var groups = new Dictionary<object, List<object>>();

        foreach (var entity in entities)
        {
            var fkValue = fkProperty.GetValue(entity);
            if (fkValue is null)
                continue;

            if (!groups.TryGetValue(fkValue, out var list))
            {
                list = [];
                groups[fkValue] = list;
            }

            list.Add(entity);
        }

        return groups;
    }

    /// <summary>
    /// Reference equality comparer for deduplication.
    /// </summary>
    internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
    {
        public static readonly ReferenceEqualityComparer<T> Instance = new();
        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>
    /// Cache entry containing instance and LRU list node.
    /// </summary>
    private readonly record struct CacheEntry(object Instance, LinkedListNode<(Type type, object key)> Node);
}
