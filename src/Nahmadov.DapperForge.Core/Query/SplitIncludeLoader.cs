using System.Reflection;

using Dapper;

using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.Core.Interfaces;
using Nahmadov.DapperForge.Core.Mapping;

namespace Nahmadov.DapperForge.Core.Query;

/// <summary>
/// Loads related entities using split queries (one query per Include).
/// </summary>
internal sealed class SplitIncludeLoader
{
    private readonly DapperDbContext _context;
    private readonly ISqlDialect _dialect;
    private readonly IdentityCache? _identityCache;

    public SplitIncludeLoader(DapperDbContext context, ISqlDialect dialect, bool useIdentityResolution)
    {
        _context = context;
        _dialect = dialect;
        _identityCache = useIdentityResolution
            ? new IdentityCache(type => _context.GetEntityMapping(type))
            : null;
    }

    public async Task LoadAsync<TEntity>(
        EntityMapping rootMapping,
        List<TEntity> roots,
        IncludeTree tree,
        CancellationToken ct = default)
        where TEntity : class
    {
        if (roots.Count == 0 || !tree.HasIncludes)
            return;

        foreach (var node in tree.Roots)
        {
            await LoadNodeAsync(rootMapping, roots.Cast<object>().ToList(), node, ct);
        }
    }

    private async Task LoadNodeAsync(
        EntityMapping parentMapping,
        List<object> parents,
        IncludeNode node,
        CancellationToken ct)
    {
        if (parents.Count == 0)
            return;

        var relatedEntities = node.IsCollection
            ? await LoadCollectionNavigationAsync(parentMapping, parents, node, ct)
            : await LoadReferenceNavigationAsync(parentMapping, parents, node, ct);

        if (relatedEntities.Count == 0 || !node.HasChildren)
            return;

        var relatedMapping = _context.GetEntityMapping(node.RelatedType);
        foreach (var childNode in node.Children)
        {
            await LoadNodeAsync(relatedMapping, relatedEntities, childNode, ct);
        }
    }

    private async Task<List<object>> LoadReferenceNavigationAsync(
        EntityMapping parentMapping,
        List<object> parents,
        IncludeNode node,
        CancellationToken ct)
    {
        var fk = parentMapping.ForeignKeys.FirstOrDefault(x => x.NavigationProperty == node.Navigation);
        if (fk is null)
            return [];

        var fkValues = ExtractDistinctValues(parents, fk.ForeignKeyProperty);
        if (fkValues.Count == 0)
            return [];

        var relatedMapping = _context.GetEntityMapping(fk.PrincipalEntityType);
        var relatedEntities = await QueryByPrimaryKeyAsync(relatedMapping, fkValues);

        var relatedIndex = IdentityCache.BuildKeyIndex(relatedMapping, relatedEntities);
        HydrateReferenceNavigation(parents, node.Navigation, fk.ForeignKeyProperty, relatedIndex);

        return relatedEntities;
    }

    private async Task<List<object>> LoadCollectionNavigationAsync(
        EntityMapping parentMapping,
        List<object> parents,
        IncludeNode node,
        CancellationToken ct)
    {
        var parentKeyProp = parentMapping.KeyProperties.FirstOrDefault()
            ?? throw new InvalidOperationException($"Entity '{parentMapping.EntityType.Name}' has no key defined.");

        var parentKeys = ExtractDistinctValues(parents, parentKeyProp);
        if (parentKeys.Count == 0)
            return [];

        var childMapping = _context.GetEntityMapping(node.RelatedType);
        var inverseFk = childMapping.ForeignKeys.FirstOrDefault(f => f.PrincipalEntityType == parentMapping.EntityType)
            ?? throw new InvalidOperationException(
                $"No foreign key found on '{childMapping.EntityType.Name}' pointing to '{parentMapping.EntityType.Name}'.");

        var children = await QueryByForeignKeyAsync(childMapping, inverseFk, parentKeys);

        var childrenByParent = IdentityCache.GroupByForeignKey(inverseFk.ForeignKeyProperty, children);
        HydrateCollectionNavigation(parents, parentKeyProp, node, childrenByParent);

        return children;
    }

    private async Task<List<object>> QueryByPrimaryKeyAsync(EntityMapping mapping, List<object> keyValues)
    {
        var (sql, parameters) = BuildInQuery(mapping, mapping.KeyProperties[0], keyValues);
        return await ExecuteQueryAsync(mapping.EntityType, sql, parameters);
    }

    private async Task<List<object>> QueryByForeignKeyAsync(
        EntityMapping mapping,
        ForeignKeyMapping fk,
        List<object> fkValues)
    {
        var (sql, parameters) = BuildInQuery(mapping, fk.ForeignKeyProperty, fkValues);
        return await ExecuteQueryAsync(mapping.EntityType, sql, parameters);
    }

    private (string sql, DynamicParameters parameters) BuildInQuery(
        EntityMapping mapping,
        PropertyInfo filterProperty,
        List<object> values)
    {
        var generator = _context.GetSqlGenerator(mapping.EntityType);
        var baseSelect = DapperDbContext.GetSelectAllSqlFromGenerator(generator);

        var propMapping = mapping.PropertyMappings.First(pm => pm.Property == filterProperty);
        var columnSql = $"a.{_dialect.QuoteIdentifier(propMapping.ColumnName)}";

        var sql = $"{baseSelect} WHERE {columnSql} IN {_dialect.FormatParameter("p")}";

        var parameters = new DynamicParameters();
        parameters.Add("p", values);

        return (sql, parameters);
    }

    private async Task<List<object>> ExecuteQueryAsync(Type entityType, string sql, DynamicParameters parameters)
    {
        var queryMethod = _context.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == "QueryAsync" && m.IsGenericMethodDefinition);

        var genericMethod = queryMethod.MakeGenericMethod(entityType);
        var task = (Task)genericMethod.Invoke(_context, [sql, parameters, null!])!;
        await task.ConfigureAwait(false);

        var resultProp = task.GetType().GetProperty("Result")!;
        var enumerable = (System.Collections.IEnumerable)resultProp.GetValue(task)!;

        var results = new List<object>();
        foreach (var item in enumerable)
        {
            var resolved = _identityCache?.Resolve(entityType, item) ?? item;
            results.Add(resolved);
        }

        return results;
    }

    private static List<object> ExtractDistinctValues(List<object> entities, PropertyInfo property)
    {
        return entities
            .Select(e => property.GetValue(e))
            .Where(v => v is not null)
            .Distinct()
            .Cast<object>()
            .ToList();
    }

    private static void HydrateReferenceNavigation(
        List<object> parents,
        PropertyInfo navigation,
        PropertyInfo fkProperty,
        Dictionary<object, object> relatedByKey)
    {
        foreach (var parent in parents)
        {
            var fkValue = fkProperty.GetValue(parent);
            if (fkValue is not null && relatedByKey.TryGetValue(fkValue, out var related))
            {
                navigation.SetValue(parent, related);
            }
        }
    }

    private static void HydrateCollectionNavigation(
        List<object> parents,
        PropertyInfo parentKeyProp,
        IncludeNode node,
        Dictionary<object, List<object>> childrenByParent)
    {
        foreach (var parent in parents)
        {
            var pk = parentKeyProp.GetValue(parent);
            if (pk is null)
                continue;

            var items = childrenByParent.TryGetValue(pk, out var list)
                ? list
                : [];

            var collection = CollectionHelper.CreateCollectionWithItems(
                node.Navigation.PropertyType,
                node.RelatedType,
                items);

            node.Navigation.SetValue(parent, collection);
        }
    }
}
