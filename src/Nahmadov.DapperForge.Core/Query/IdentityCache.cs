using System.Reflection;
using System.Runtime.CompilerServices;

using Nahmadov.DapperForge.Core.Mapping;

namespace Nahmadov.DapperForge.Core.Query;

/// <summary>
/// Manages identity resolution for Include operations.
/// Ensures the same entity key returns the same instance.
/// Uses LRU eviction to prevent unbounded memory growth.
/// </summary>
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
    /// </summary>
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
