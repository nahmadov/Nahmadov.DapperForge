using System.Text;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;

namespace Nahmadov.DapperForge.Core.Querying.Includes;
/// <summary>
/// Builds SQL query plans for single-query Include operations using JOINs.
/// </summary>
internal sealed class SingleQueryPlanBuilder(ISqlDialect dialect, Func<Type, EntityMapping> resolveMapping)
{
    private readonly ISqlDialect _dialect = dialect;
    private readonly Func<Type, EntityMapping> _resolveMapping = resolveMapping;
    private int _aliasIndex;

    public SingleQueryPlan Build(EntityMapping rootMapping, IncludeTree tree)
    {
        _aliasIndex = 0;

        var selectParts = new List<string>();
        var joinParts = new List<string>();
        var splitOnColumns = new List<string>();

        const string rootAlias = "a";
        AppendSelectColumns(selectParts, rootAlias, rootMapping);

        var rootContext = new JoinContext(rootAlias, rootMapping);
        foreach (var node in tree.Roots)
        {
            BuildNodeJoin(node, rootContext, selectParts, joinParts, splitOnColumns);
        }

        var sql = BuildFinalSql(rootMapping, rootAlias, selectParts, joinParts);

        // Count all types recursively (root + all includes at all levels)
        var totalTypeCount = 1 + CountNodesRecursive(tree.Roots);

        return new SingleQueryPlan
        {
            Sql = sql,
            SplitOn = string.Join(", ", splitOnColumns),
            MapTypesCount = totalTypeCount
        };
    }

    /// <summary>
    /// Recursively counts total number of nodes in the Include tree.
    /// Used to determine the correct type count for Dapper multi-mapping.
    /// </summary>
    private static int CountNodesRecursive(IReadOnlyList<IncludeNode> nodes)
    {
        var count = nodes.Count;
        foreach (var node in nodes)
        {
            if (node.HasChildren)
            {
                count += CountNodesRecursive(node.Children);
            }
        }
        return count;
    }

    private void BuildNodeJoin(
        IncludeNode node,
        JoinContext parentContext,
        List<string> selectParts,
        List<string> joinParts,
        List<string> splitOnColumns)
    {
        var alias = GetNextAlias();
        var relatedMapping = _resolveMapping(node.RelatedType);

        var joinClause = node.IsCollection
            ? BuildCollectionJoin(node, parentContext, relatedMapping, alias)
            : BuildReferenceJoin(node, parentContext, relatedMapping, alias);

        joinParts.Add(joinClause);
        var splitColumn = AppendSelectColumns(selectParts, alias, relatedMapping);
        splitOnColumns.Add(splitColumn);

        var currentContext = new JoinContext(alias, relatedMapping);
        foreach (var child in node.Children)
        {
            BuildNodeJoin(child, currentContext, selectParts, joinParts, splitOnColumns);
        }
    }

    private string BuildReferenceJoin(
        IncludeNode node,
        JoinContext parentContext,
        EntityMapping relatedMapping,
        string alias)
    {
        var fk = parentContext.Mapping.ForeignKeys
            .FirstOrDefault(f => f.NavigationProperty == node.Navigation)
            ?? throw new InvalidOperationException($"No FK found for navigation '{node.Navigation.Name}'.");

        var fkMapping = parentContext.Mapping.PropertyMappings
            .First(pm => pm.Property == fk.ForeignKeyProperty);

        var parentFkColumn = FormatColumn(parentContext.Alias, fkMapping.ColumnName);
        var relatedPkColumn = FormatColumn(alias, fk.PrincipalKeyColumnName);

        return $"LEFT JOIN {FormatTable(relatedMapping)} {_dialect.FormatTableAlias(alias)} ON {relatedPkColumn} = {parentFkColumn}";
    }

    private string BuildCollectionJoin(
        IncludeNode node,
        JoinContext parentContext,
        EntityMapping relatedMapping,
        string alias)
    {
        var inverseFk = relatedMapping.ForeignKeys
            .FirstOrDefault(f => f.PrincipalEntityType == parentContext.Mapping.EntityType)
            ?? throw new InvalidOperationException($"No inverse FK found for collection '{node.Navigation.Name}'.");

        var childFkMapping = relatedMapping.PropertyMappings
            .First(pm => pm.Property == inverseFk.ForeignKeyProperty);

        var parentKeyProp = parentContext.Mapping.KeyProperties.First();
        var parentKeyMapping = parentContext.Mapping.PropertyMappings
            .First(pm => pm.Property == parentKeyProp);

        var childFkColumn = FormatColumn(alias, childFkMapping.ColumnName);
        var parentPkColumn = FormatColumn(parentContext.Alias, parentKeyMapping.ColumnName);

        return $"LEFT JOIN {FormatTable(relatedMapping)} {_dialect.FormatTableAlias(alias)} ON {childFkColumn} = {parentPkColumn}";
    }

    private string AppendSelectColumns(List<string> selectParts, string alias, EntityMapping mapping)
    {
        string? firstColumnAlias = null;
        foreach (var pm in mapping.PropertyMappings)
        {
            var column = FormatColumn(alias, pm.ColumnName);
            var columnAlias = $"{alias}__{pm.Property.Name}";
            firstColumnAlias ??= columnAlias;
            selectParts.Add($"{column} AS {_dialect.QuoteIdentifier(columnAlias)}");
        }
        return firstColumnAlias!;
    }

    private string BuildFinalSql(
        EntityMapping rootMapping,
        string rootAlias,
        List<string> selectParts,
        List<string> joinParts)
    {
        var sb = new StringBuilder();

        sb.Append("SELECT ");
        sb.Append(string.Join(", ", selectParts));
        sb.Append(" FROM ");
        sb.Append(FormatTable(rootMapping));
        sb.Append(' ');
        sb.Append(_dialect.FormatTableAlias(rootAlias));

        foreach (var join in joinParts)
        {
            sb.Append(' ');
            sb.Append(join);
        }

        return sb.ToString();
    }

    private string GetNextAlias()
    {
        _aliasIndex++;
        return $"b{_aliasIndex}";
    }

    private string FormatColumn(string alias, string columnName)
    {
        return $"{alias}.{_dialect.QuoteIdentifier(columnName)}";
    }

    private string FormatTable(EntityMapping mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping.Schema))
            return _dialect.QuoteIdentifier(mapping.TableName);

        return $"{_dialect.QuoteIdentifier(mapping.Schema)}.{_dialect.QuoteIdentifier(mapping.TableName)}";
    }

    private sealed record JoinContext(string Alias, EntityMapping Mapping);
}


