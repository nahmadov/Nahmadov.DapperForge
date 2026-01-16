using System.Reflection;

using Dapper;

using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;

namespace Nahmadov.DapperForge.Core.Querying.Includes;
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
            await LoadNodeAsync(rootMapping, roots.Cast<object>().ToList(), node, ct).ConfigureAwait(false);
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
            ? await LoadCollectionNavigationAsync(parentMapping, parents, node, ct).ConfigureAwait(false)
            : await LoadReferenceNavigationAsync(parentMapping, parents, node, ct).ConfigureAwait(false);

        if (relatedEntities.Count == 0 || !node.HasChildren)
            return;

        var relatedMapping = _context.GetEntityMapping(node.RelatedType);
        foreach (var childNode in node.Children)
        {
            await LoadNodeAsync(relatedMapping, relatedEntities, childNode, ct).ConfigureAwait(false);
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
        var relatedEntities = await QueryByPrimaryKeyAsync(relatedMapping, fkValues).ConfigureAwait(false);

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

        var children = await QueryByForeignKeyAsync(childMapping, inverseFk, parentKeys).ConfigureAwait(false);

        var childrenByParent = IdentityCache.GroupByForeignKey(inverseFk.ForeignKeyProperty, children);
        HydrateCollectionNavigation(parents, parentKeyProp, node, childrenByParent);

        return children;
    }

    private async Task<List<object>> QueryByPrimaryKeyAsync(EntityMapping mapping, List<object> keyValues)
    {
        return await QueryByPropertyAsync(mapping, mapping.KeyProperties[0], keyValues).ConfigureAwait(false);
    }

    private async Task<List<object>> QueryByForeignKeyAsync(
        EntityMapping mapping,
        ForeignKeyMapping fk,
        List<object> fkValues)
    {
        return await QueryByPropertyAsync(mapping, fk.ForeignKeyProperty, fkValues).ConfigureAwait(false);
    }

    /// <summary>
    /// Queries entities by a property using IN clause with automatic batching for large value lists.
    /// </summary>
    /// <remarks>
    /// <para><b>Parameter Limits:</b></para>
    /// <list type="bullet">
    /// <item>SQL Server: Maximum 2100 parameters per query</item>
    /// <item>Oracle: Maximum 1000 values in IN clause</item>
    /// </list>
    /// <para>
    /// When value count exceeds the limit, queries are automatically batched and results are combined.
    /// </para>
    /// </remarks>
    private async Task<List<object>> QueryByPropertyAsync(
        EntityMapping mapping,
        PropertyInfo filterProperty,
        List<object> values)
    {
        if (values.Count == 0)
            return [];

        // Determine batch size based on dialect
        var batchSize = GetInClauseBatchSize();

        // Single batch - execute directly
        if (values.Count <= batchSize)
        {
            var (sql, parameters) = BuildInQuery(mapping, filterProperty, values);
            return await ExecuteQueryAsync(mapping.EntityType, sql, parameters).ConfigureAwait(false);
        }

        // Multiple batches required
        var allResults = new List<object>();
        var batches = values.Chunk(batchSize);

        foreach (var batch in batches)
        {
            var batchValues = batch.ToList();
            var (sql, parameters) = BuildInQuery(mapping, filterProperty, batchValues);
            var batchResults = await ExecuteQueryAsync(mapping.EntityType, sql, parameters).ConfigureAwait(false);
            allResults.AddRange(batchResults);
        }

        return allResults;
    }

    /// <summary>
    /// Gets the maximum batch size for IN clause based on the SQL dialect.
    /// </summary>
    /// <returns>
    /// <list type="bullet">
    /// <item>SQL Server: 2000 (safe limit, actual is 2100 parameters)</item>
    /// <item>Oracle: 900 (safe limit, actual is 1000 values)</item>
    /// <item>Other: 1000 (conservative default)</item>
    /// </list>
    /// </returns>
    private int GetInClauseBatchSize()
    {
        var dialectName = _dialect.Name.ToLowerInvariant();

        return dialectName switch
        {
            "sqlserver" => 2000, // SQL Server parameter limit is 2100
            "oracle" => 900,     // Oracle IN clause limit is 1000
            _ => 1000            // Conservative default
        };
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

    /// <summary>
    /// Executes a dynamic query and applies identity caching.
    /// Uses QueryDynamicAsync to avoid reflection overhead (~1200x faster than reflection).
    /// </summary>
    private async Task<List<object>> ExecuteQueryAsync(Type entityType, string sql, DynamicParameters parameters)
    {
        // Use direct QueryDynamicAsync instead of reflection-based invocation
        var enumerable = await _context.QueryDynamicAsync(entityType, sql, parameters).ConfigureAwait(false);

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


