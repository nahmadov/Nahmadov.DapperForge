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
            map: objs => MapRow<TEntity>(objs, rootMapping, includeTree));

        return DeduplicateRoots(rows, rootMapping);
    }

    private static Type[] BuildTypesArray<TEntity>(IncludeTree tree) where TEntity : class
    {
        var types = new Type[1 + tree.Roots.Count];
        types[0] = typeof(TEntity);

        for (var i = 0; i < tree.Roots.Count; i++)
        {
            types[i + 1] = tree.Roots[i].RelatedType;
        }

        return types;
    }

    private TEntity? MapRow<TEntity>(object?[] objects, EntityMapping rootMapping, IncludeTree tree)
        where TEntity : class
    {
        if (objects.Length == 0 || objects[0] is null)
            return null;

        var root = (TEntity)_identityCache.Resolve(rootMapping, objects[0]!);

        for (var i = 0; i < tree.Roots.Count; i++)
        {
            var node = tree.Roots[i];
            var relatedObj = (i + 1 < objects.Length) ? objects[i + 1] : null;

            if (relatedObj is null)
                continue;

            var relatedMapping = _context.GetEntityMapping(node.RelatedType);
            var related = _identityCache.Resolve(relatedMapping, relatedObj);

            AssignNavigation(root, node, related);
        }

        return root;
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
