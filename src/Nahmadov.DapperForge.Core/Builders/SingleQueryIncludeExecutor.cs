using System.Reflection;
using System.Runtime.CompilerServices;

using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.Core.Mapping;
using Nahmadov.DapperForge.Core.Query;

namespace Nahmadov.DapperForge.Core.Builders;

/// <summary>
/// Executes AsSingleQuery include queries (root + first-level includes) and performs graph fixup:
/// - Identity resolution (same entity key => same instance)
/// - Reference navigation assignment
/// - Collection navigation accumulation (JOIN duplicates handled)
/// </summary>
internal sealed class SingleQueryIncludeExecutor
{
    private readonly DapperDbContext _context;

    // (Type, Key) -> instance
    private readonly Dictionary<(Type type, object key), object> _identityCache = new();

    public SingleQueryIncludeExecutor(DapperDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<TEntity>> ExecuteAsync<TEntity>(
        string sql,
        object parameters,
        string splitOn,
        EntityMapping rootMapping,
        IncludeTree includeTree,
        CancellationToken ct = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(rootMapping);
        ArgumentNullException.ThrowIfNull(includeTree);

        // Types: root + each first-level include related type (same order as includeTree.Roots)
        var types = BuildTypesArray<TEntity>(includeTree);

        // Row mapper returns root instance (possibly identity-resolved) for each joined row
        var rows = await _context.QueryWithTypesAsync<TEntity?>(
            sql: sql,
            types: types,
            parameters: parameters,
            splitOn: splitOn,
            map: objs => MapRow<TEntity>(objs, rootMapping, includeTree));

        // Deduplicate roots by key (JOIN duplicates)
        return DeduplicateRoots(rows, rootMapping);
    }

    private static Type[] BuildTypesArray<TEntity>(IncludeTree tree)
        where TEntity : class
    {
        var list = new List<Type>(1 + tree.Roots.Count) { typeof(TEntity) };
        foreach (var node in tree.Roots)
            list.Add(node.RelatedType);

        return list.ToArray();
    }

    private TEntity? MapRow<TEntity>(
        object?[] objs,
        EntityMapping rootMapping,
        IncludeTree tree)
        where TEntity : class
    {
        if (objs.Length == 0 || objs[0] is null)
            return null;

        // Root
        var rootObj = objs[0]!;
        var root = (TEntity)ResolveIdentity(rootMapping, rootObj);

        // Attach first-level includes
        for (var i = 0; i < tree.Roots.Count; i++)
        {
            var node = tree.Roots[i];
            var relatedObj = (i + 1 < objs.Length) ? objs[i + 1] : null;
            if (relatedObj is null)
                continue;

            var relMapping = _context.GetEntityMapping(node.RelatedType);
            var related = ResolveIdentity(relMapping, relatedObj);

            if (!node.IsCollection)
            {
                // Reference navigation
                node.Navigation.SetValue(root, related);
            }
            else
            {
                // Collection navigation: accumulate
                var current = node.Navigation.GetValue(root);
                if (current is null)
                {
                    current = CreateCollection(node.Navigation.PropertyType, node.RelatedType);
                    node.Navigation.SetValue(root, current);
                }

                AddToCollection(current, related);
            }
        }

        return root;
    }

    private object ResolveIdentity(EntityMapping mapping, object instance)
    {
        var keyProp = mapping.KeyProperties.FirstOrDefault();
        if (keyProp is null)
            return instance;

        var key = keyProp.GetValue(instance);
        if (key is null)
            return instance;

        var cacheKey = (mapping.EntityType, key);
        if (_identityCache.TryGetValue(cacheKey, out var existing))
            return existing;

        _identityCache[cacheKey] = instance;
        return instance;
    }

    private static List<TEntity> DeduplicateRoots<TEntity>(List<TEntity?> rows, EntityMapping rootMapping)
        where TEntity : class
    {
        var rootKeyProp = rootMapping.KeyProperties.FirstOrDefault();

        // If no key, fallback to distinct by reference
        if (rootKeyProp is null)
        {
            return rows
                .Where(r => r is not null)
                .Cast<TEntity>()
                .Distinct(ReferenceEqualityComparer<TEntity>.Instance)
                .ToList();
        }

        var dict = new Dictionary<object, TEntity>();

        foreach (var r in rows)
        {
            if (r is null) continue;

            var key = rootKeyProp.GetValue(r);
            if (key is null) continue;

            dict[key] = r;
        }

        return dict.Values.ToList();
    }

    private static object CreateCollection(Type propertyType, Type elementType)
    {
        // If interface (ICollection<T>, IEnumerable<T>), create List<T>
        if (propertyType.IsInterface)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            return Activator.CreateInstance(listType)
                   ?? throw new InvalidOperationException($"Cannot create collection '{listType.Name}'.");
        }

        // If concrete type has parameterless ctor
        var instance = Activator.CreateInstance(propertyType);
        if (instance is not null)
            return instance;

        // Fallback: List<T>
        var fallback = typeof(List<>).MakeGenericType(elementType);
        return Activator.CreateInstance(fallback)
               ?? throw new InvalidOperationException($"Cannot create collection '{fallback.Name}'.");
    }

    private static void AddToCollection(object collection, object item)
    {
        // Non-generic IList (works for List<T> created via reflection)
        if (collection is System.Collections.IList list)
        {
            list.Add(item);
            return;
        }

        // Try ICollection<T>.Add via reflection
        var collType = collection.GetType();
        var addMethod = collType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
        if (addMethod is not null)
        {
            addMethod.Invoke(collection, new[] { item });
            return;
        }

        throw new NotSupportedException($"Collection type '{collType.Name}' does not support Add().");
    }

    private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
    {
        public static readonly ReferenceEqualityComparer<T> Instance = new();
        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
