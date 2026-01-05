using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.Core.Mapping;
using Nahmadov.DapperForge.Core.Query;

namespace Nahmadov.DapperForge.Core.Builders;

/// <summary>
/// Executes single-query Include operations using JOIN and performs graph fixup.
/// Handles identity resolution and navigation property assignment.
/// </summary>
internal sealed class SingleQueryIncludeExecutor
{
    private readonly DapperDbContext _context;
    private readonly IdentityCache _identityCache;

    public SingleQueryIncludeExecutor(DapperDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _identityCache = new IdentityCache(type => _context.GetEntityMapping(type));
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

        var types = BuildTypesArray<TEntity>(includeTree);

        var rows = await _context.QueryWithTypesAsync<TEntity?>(
            sql: sql,
            types: types,
            parameters: parameters,
            splitOn: splitOn,
            map: objs => MapRow<TEntity>(objs, rootMapping, includeTree)).ConfigureAwait(false);

        return DeduplicateRoots(rows, rootMapping);
    }

    /// <summary>
    /// Builds array of types for Dapper's multi-mapping.
    /// Recursively collects all types from Include/ThenInclude chain.
    /// </summary>
    private static Type[] BuildTypesArray<TEntity>(IncludeTree tree) where TEntity : class
    {
        var types = new List<Type> { typeof(TEntity) };
        CollectTypesRecursive(tree.Roots, types);
        return types.ToArray();
    }

    /// <summary>
    /// Recursively collects all related entity types from the Include tree.
    /// This ensures multi-level ThenInclude chains are properly supported.
    /// </summary>
    private static void CollectTypesRecursive(IReadOnlyList<IncludeNode> nodes, List<Type> types)
    {
        foreach (var node in nodes)
        {
            types.Add(node.RelatedType);

            if (node.HasChildren)
            {
                CollectTypesRecursive(node.Children, types);
            }
        }
    }

    /// <summary>
    /// Maps flattened Dapper result row to hierarchical object graph.
    /// Handles multi-level ThenInclude chains by recursively processing the objects array.
    /// </summary>
    private TEntity? MapRow<TEntity>(object?[] objects, EntityMapping rootMapping, IncludeTree tree)
        where TEntity : class
    {
        if (objects.Length == 0 || objects[0] is null)
            return null;

        var root = (TEntity)_identityCache.Resolve(rootMapping, objects[0]!);

        var objectIndex = 1; // Start after root entity
        foreach (var node in tree.Roots)
        {
            objectIndex = MapNodeRecursive(root, node, objects, objectIndex);
        }

        return root;
    }

    /// <summary>
    /// Recursively maps Include nodes to navigation properties.
    /// Returns the next index in the objects array after processing this node and its children.
    /// </summary>
    private int MapNodeRecursive(object parent, IncludeNode node, object?[] objects, int currentIndex)
    {
        if (currentIndex >= objects.Length)
            return currentIndex;

        var relatedObj = objects[currentIndex];
        currentIndex++; // Move to next index for siblings/children

        if (relatedObj is null)
        {
            // Skip null object but still process its children (they will all be at the same index positions)
            foreach (var child in node.Children)
            {
                currentIndex = MapNodeRecursive(relatedObj!, child, objects, currentIndex);
            }
            return currentIndex;
        }

        var relatedMapping = _context.GetEntityMapping(node.RelatedType);
        var related = _identityCache.Resolve(relatedMapping, relatedObj);

        AssignNavigation(parent, node, related);

        // Recursively process children (ThenInclude)
        foreach (var child in node.Children)
        {
            currentIndex = MapNodeRecursive(related, child, objects, currentIndex);
        }

        return currentIndex;
    }

    private static void AssignNavigation<TEntity>(TEntity root, IncludeNode node, object related)
        where TEntity : class
    {
        if (!node.IsCollection)
        {
            node.Navigation.SetValue(root, related);
            return;
        }

        var current = node.Navigation.GetValue(root);
        if (current is null)
        {
            current = CollectionHelper.CreateCollection(node.Navigation.PropertyType, node.RelatedType);
            node.Navigation.SetValue(root, current);
        }

        CollectionHelper.AddToCollection(current, related);
    }

    private static List<TEntity> DeduplicateRoots<TEntity>(List<TEntity?> rows, EntityMapping rootMapping)
        where TEntity : class
    {
        var keyProp = rootMapping.KeyProperties.FirstOrDefault();

        if (keyProp is null)
        {
            return rows
                .Where(r => r is not null)
                .Cast<TEntity>()
                .Distinct(IdentityCache.ReferenceEqualityComparer<TEntity>.Instance)
                .ToList();
        }

        var uniqueByKey = new Dictionary<object, TEntity>();

        foreach (var row in rows)
        {
            if (row is null)
                continue;

            var key = keyProp.GetValue(row);
            if (key is not null)
                uniqueByKey[key] = row;
        }

        return [.. uniqueByKey.Values];
    }
}
