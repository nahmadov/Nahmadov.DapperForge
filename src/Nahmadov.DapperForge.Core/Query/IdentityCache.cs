using System.Reflection;
using System.Runtime.CompilerServices;

using Nahmadov.DapperForge.Core.Mapping;

namespace Nahmadov.DapperForge.Core.Query;

/// <summary>
/// Manages identity resolution for Include operations.
/// Ensures the same entity key returns the same instance.
/// </summary>
internal sealed class IdentityCache(Func<Type, EntityMapping> resolveMapping)
{
    private readonly Dictionary<(Type type, object key), object> _cache = [];
    private readonly Func<Type, EntityMapping> _resolveMapping = resolveMapping;

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
    /// Gets or adds an instance to the cache.
    /// </summary>
    public object GetOrAdd(Type type, object key, object instance)
    {
        var cacheKey = (type, key);
        if (_cache.TryGetValue(cacheKey, out var existing))
            return existing;

        _cache[cacheKey] = instance;
        return instance;
    }

    /// <summary>
    /// Tries to get an existing instance from the cache.
    /// </summary>
    public bool TryGet(Type type, object key, out object? instance)
    {
        return _cache.TryGetValue((type, key), out instance);
    }

    /// <summary>
    /// Clears the identity cache.
    /// </summary>
    public void Clear() => _cache.Clear();

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
}
