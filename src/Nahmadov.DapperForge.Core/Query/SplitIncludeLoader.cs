using System.Reflection;

using Dapper;

using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.Core.Interfaces;
using Nahmadov.DapperForge.Core.Mapping;

namespace Nahmadov.DapperForge.Core.Query;

internal sealed class SplitIncludeLoader
{
    private readonly DapperDbContext _context;
    private readonly ISqlDialect _dialect;
    private readonly bool _identityResolution;

    private readonly Dictionary<(Type type, object key), object> _identityCache = [];

    public SplitIncludeLoader(DapperDbContext context, ISqlDialect dialect, bool identityResolution)
    {
        _context = context;
        _dialect = dialect;
        _identityResolution = identityResolution;
    }

    public async Task LoadAsync<TEntity>(
        EntityMapping rootMapping,
        List<TEntity> roots,
        IncludeTree tree,
        CancellationToken ct = default)
        where TEntity : class
    {
        foreach (var node in tree.Roots)
        {
            await LoadNodeAsync(rootMapping, roots, node, ct);
        }
    }

    private async Task LoadNodeAsync<TParent>(
        EntityMapping parentMapping,
        List<TParent> parents,
        IncludeNode node,
        CancellationToken ct)
        where TParent : class
    {
        if (parents.Count == 0)
            return;

        // NOTE: Current FK model supports reference navigations best.
        if (node.IsCollection)
        {
            // TODO: Requires inverse relationship metadata (child FK -> parent PK).
            // For now, keep behavior explicit.
            throw new NotSupportedException(
                $"Collection Include '{node.Navigation.Name}' is not supported yet with the current ForeignKeyMapping model.");
        }

        var fk = parentMapping.ForeignKeys.FirstOrDefault(x => x.NavigationProperty == node.Navigation);
        if (fk is null)
            return;

        var fkValues = parents
            .Select(p => fk.ForeignKeyProperty.GetValue(p))
            .Where(v => v is not null)
            .Distinct()
            .Cast<object>()
            .ToList();

        if (fkValues.Count == 0)
            return;

        var relatedMapping = _context.GetEntityMapping(fk.PrincipalEntityType);

        // Build: SELECT <cols> FROM <schema.table> a WHERE a.<PK_COLUMN> IN (...)
        var (sql, parameters) = BuildRelatedQuery(relatedMapping, fk, fkValues);

        // Execute via DapperDbContext.QueryAsync<TRelated> dynamically
        var relatedRows = await QueryRelatedAsync(fk.PrincipalEntityType, sql, parameters);

        // Index by related key
        var relatedIndex = BuildIndexByKey(relatedMapping, relatedRows);

        // Hydrate reference navigation
        HydrateReference(parents, node.Navigation, fk.ForeignKeyProperty, relatedIndex);

        // ThenInclude: children
        if (node.Children.Count > 0 && relatedRows.Count > 0)
        {
            foreach (var child in node.Children)
            {
                // We have "object" list; call generic method via reflection to keep it simple.
                await LoadChildrenAsync(relatedMapping, relatedRows, child, ct);
            }
        }
    }

    private async Task LoadChildrenAsync(
        EntityMapping parentMapping,
        List<object> parents,
        IncludeNode child,
        CancellationToken ct)
    {
        // Convert List<object> -> List<TParent> for generic method
        var parentType = parentMapping.EntityType;

        var listType = typeof(List<>).MakeGenericType(parentType);
        var typedList = (System.Collections.IList)Activator.CreateInstance(listType)!;
        foreach (var p in parents)
            typedList.Add(p);

        var mi = GetType()
            .GetMethod(nameof(LoadNodeAsync), BindingFlags.Instance | BindingFlags.NonPublic)!;

        var g = mi.MakeGenericMethod(parentType);
        var task = (Task)g.Invoke(this, [parentMapping, typedList, child, ct])!;
        await task.ConfigureAwait(false);
    }

    private (string sql, DynamicParameters parameters) BuildRelatedQuery(
        EntityMapping relatedMapping,
        ForeignKeyMapping fk,
        IReadOnlyList<object> fkValues)
    {
        var relatedGenerator = _context.GetSqlGenerator(relatedMapping.EntityType);
        var baseSelect = DapperDbContext.GetSelectAllSqlFromGenerator(relatedGenerator);

        if (relatedMapping.KeyProperties.Count == 0)
            throw new InvalidOperationException($"Related entity '{relatedMapping.EntityType.Name}' has no key defined.");

        var keyProp = relatedMapping.KeyProperties[0];
        var keyMap = relatedMapping.PropertyMappings.First(pm => pm.Property == keyProp);
        var pkColumnSql = $"a.{_dialect.QuoteIdentifier(keyMap.ColumnName)}";
        var paramName = "p";
        var inClause = $"{pkColumnSql} IN {_dialect.FormatParameter(paramName)}";

        var parameters = new DynamicParameters();
        parameters.Add(paramName, fkValues);

        var sql = $"{baseSelect} WHERE {inClause}";
        return (sql, parameters);
    }

    private async Task<List<object>> QueryRelatedAsync(Type relatedType, string sql, DynamicParameters parameters)
    {
        // Find QueryAsync<T> on context
        var queryMethod = _context.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == "QueryAsync" && m.IsGenericMethodDefinition);

        var generic = queryMethod.MakeGenericMethod(relatedType);

        var task = (Task)generic.Invoke(_context, new object[] { sql, parameters, null! })!;
        await task.ConfigureAwait(false);

        var resultProp = task.GetType().GetProperty("Result")!;
        var enumerable = (System.Collections.IEnumerable)resultProp.GetValue(task)!;

        var list = new List<object>();
        foreach (var item in enumerable)
        {
            if (_identityResolution)
                list.Add(ResolveIdentity(relatedType, item));
            else
                list.Add(item);
        }

        return list;
    }

    private object ResolveIdentity(Type type, object instance)
    {
        // Find key property by convention from mapping
        var mapping = _context.GetEntityMapping(type);
        if (mapping.KeyProperties.Count == 0)
            return instance;

        var keyProp = mapping.KeyProperties[0];
        var keyVal = keyProp.GetValue(instance);
        if (keyVal is null)
            return instance;

        var k = (type, keyVal);
        if (_identityCache.TryGetValue(k, out var existing))
            return existing;

        _identityCache[k] = instance;
        return instance;
    }

    private Dictionary<object, object> BuildIndexByKey(EntityMapping relatedMapping, List<object> relatedRows)
    {
        var dict = new Dictionary<object, object>();

        if (relatedMapping.KeyProperties.Count == 0)
            return dict;

        var keyProp = relatedMapping.KeyProperties[0];

        foreach (var row in relatedRows)
        {
            var key = keyProp.GetValue(row);
            if (key is not null)
                dict[key] = row;
        }

        return dict;
    }

    private void HydrateReference<TParent>(
        List<TParent> parents,
        PropertyInfo navigationProperty,
        PropertyInfo fkProperty,
        Dictionary<object, object> relatedByKey)
        where TParent : class
    {
        foreach (var parent in parents)
        {
            var fkValue = fkProperty.GetValue(parent);
            if (fkValue is null)
                continue;

            if (relatedByKey.TryGetValue(fkValue, out var related))
            {
                navigationProperty.SetValue(parent, related);
            }
        }
    }
}