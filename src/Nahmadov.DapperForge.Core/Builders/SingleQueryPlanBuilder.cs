using System.Text;

using Nahmadov.DapperForge.Core.Interfaces;
using Nahmadov.DapperForge.Core.Mapping;
using Nahmadov.DapperForge.Core.Query;

namespace Nahmadov.DapperForge.Core.Builders;

internal sealed class JoinContext
{
    internal required string ParentAlias { get; init; }
    internal required EntityMapping ParentMapping { get; init; }
}

internal class SingleQueryPlanBuilder(ISqlDialect dialect, Func<Type, EntityMapping> resolveMapping)
{
    private readonly ISqlDialect _dialect = dialect;
    private readonly Func<Type, EntityMapping> _resolveMapping = resolveMapping;
    private int _aliasIndex = 0;

    internal SingleQueryPlan Build(EntityMapping rootMapping, IncludeTree tree)
    {
        var rootAlias = "a";

        var sb = new StringBuilder();
        var selectParts = new List<string>();
        var joinParts = new List<string>();

        AppendSelectColumns(selectParts, rootAlias, "a", rootMapping);

        var splitOnCols = new List<string>();

        foreach (var node in tree.Roots)
        {
            BuildNode(
                node,
                new JoinContext
                {
                    ParentAlias = rootAlias,
                    ParentMapping = rootMapping
                },
                selectParts,
                joinParts,
                splitOnCols
            );
        }
        var rootTable = FullTable(rootMapping);
        sb.Append("SELECT ");
        sb.Append(string.Join(", ", selectParts));
        sb.Append(" FROM ");
        sb.Append(rootTable);
        sb.Append(' ');
        sb.Append(_dialect.FormatTableAlias(rootAlias));
        sb.Append(' ');

        foreach (var j in joinParts)
        {
            sb.Append(j);
            sb.Append(' ');
        }
        return new SingleQueryPlan
        {
            Sql = sb.ToString().Trim(),
            SplitOn = string.Join(", ", splitOnCols),
            MapTypesCount = 1 + tree.Roots.Count
        };
    }

    private void BuildNode(IncludeNode node, JoinContext ctx, List<string> selectParts, List<string> joinParts, List<string> splitOnCols)
    {
        _aliasIndex++;
        var alias = $"b{_aliasIndex}";
        var relMapping = _resolveMapping(node.RelatedType);

        if (!node.IsCollection)
        {
            // Reference
            var fk = ctx.ParentMapping.ForeignKeys
                .FirstOrDefault(f => f.NavigationProperty == node.Navigation);

            if (fk is null)
                throw new InvalidOperationException(
                    $"No FK for navigation '{node.Navigation.Name}'.");

            var fkMap = ctx.ParentMapping.PropertyMappings
                .First(pm => pm.Property == fk.ForeignKeyProperty);

            var parentFk = $"{ctx.ParentAlias}.{_dialect.QuoteIdentifier(fkMap.ColumnName)}";
            var relPk = $"{alias}.{_dialect.QuoteIdentifier(fk.PrincipalKeyColumnName)}";

            joinParts.Add(
                $"LEFT JOIN {FullTable(relMapping)} {_dialect.FormatTableAlias(alias)} ON {relPk} = {parentFk}");
        }
        else
        {
            // Collection
            var invFk = relMapping.ForeignKeys
                .FirstOrDefault(f => f.PrincipalEntityType == ctx.ParentMapping.EntityType);

            if (invFk is null)
                throw new InvalidOperationException(
                    $"No inverse FK for collection '{node.Navigation.Name}'.");

            var fkMap = relMapping.PropertyMappings
                .First(pm => pm.Property == invFk.ForeignKeyProperty);

            var childFk = $"{alias}.{_dialect.QuoteIdentifier(fkMap.ColumnName)}";

            var parentPkProp = ctx.ParentMapping.KeyProperties.First();
            var parentPkMap = ctx.ParentMapping.PropertyMappings
                .First(pm => pm.Property == parentPkProp);

            var parentPk = $"{ctx.ParentAlias}.{_dialect.QuoteIdentifier(parentPkMap.ColumnName)}";

            joinParts.Add(
                $"LEFT JOIN {FullTable(relMapping)} {_dialect.FormatTableAlias(alias)} ON {childFk} = {parentPk}");
        }

        // SELECT columns
        AppendSelectColumns(selectParts, alias, alias, relMapping);

        // splitOn = PK alias
        var pkProp = relMapping.KeyProperties.First();
        splitOnCols.Add($"{alias}__{pkProp.Name}");

        // 🔁 RECURSION (ThenInclude)
        foreach (var child in node.Children)
        {
            BuildNode(
                child,
                new JoinContext
                {
                    ParentAlias = alias,
                    ParentMapping = relMapping
                },
                selectParts,
                joinParts,
                splitOnCols
            );
        }
    }

    private void AppendSelectColumns(List<string> selectParts, string tableAlias, string prefix, EntityMapping mapping)
    {
        foreach (var pm in mapping.PropertyMappings)
        {
            // column SQL: a."COL" AS a__COL
            var colSql = $"{tableAlias}.{_dialect.QuoteIdentifier(pm.ColumnName)}";
            var alias = $"{prefix}__{pm.Property.Name}";
            selectParts.Add($"{colSql} AS {_dialect.QuoteIdentifier(alias)}");
        }
    }

    private string FullTable(EntityMapping mapping)
    {
        if (!string.IsNullOrWhiteSpace(mapping.Schema))
            return $"{_dialect.QuoteIdentifier(mapping.Schema)}.{_dialect.QuoteIdentifier(mapping.TableName)}";

        return _dialect.QuoteIdentifier(mapping.TableName);
    }
}