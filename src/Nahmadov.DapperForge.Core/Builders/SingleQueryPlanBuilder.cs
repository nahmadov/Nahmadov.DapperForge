using System.Text;

using Nahmadov.DapperForge.Core.Interfaces;
using Nahmadov.DapperForge.Core.Mapping;
using Nahmadov.DapperForge.Core.Query;

namespace Nahmadov.DapperForge.Core.Builders;

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
            _aliasIndex++;
            var relAlias = $"b{_aliasIndex}";
            var relMapping = null as EntityMapping;

            if (!node.IsCollection)
            {
                var fk = rootMapping.ForeignKeys.FirstOrDefault(f => f.NavigationProperty == node.Navigation);
                if (fk is null) continue;
                relMapping = _resolveMapping(fk.PrincipalEntityType);

                var relTable = FullTable(relMapping);
                var pkCol = $"{relAlias}.{_dialect.QuoteIdentifier(fk.PrincipalKeyColumnName)}";
                var fkMap = rootMapping.PropertyMappings.First(pm => pm.Property == fk.ForeignKeyProperty);
                var fkCol = $"{rootAlias}.{_dialect.QuoteIdentifier(fkMap.ColumnName)}";

                joinParts.Add($"LEFT JOIN {relTable} {_dialect.FormatTableAlias(relAlias)} ON {pkCol} = {fkCol}");

            }
            else
            {
                relMapping = _resolveMapping(node.RelatedType);

                var invFk = relMapping.ForeignKeys.FirstOrDefault(f => f.PrincipalEntityType == rootMapping.EntityType);
                if (invFk is null)
                    throw new InvalidOperationException($"No FK found on '{relMapping.EntityType.Name}' pointing to '{rootMapping.EntityType.Name}'.");

                var relTable = FullTable(relMapping);

                // child.FK = parent.PK
                var childFkMap = relMapping.PropertyMappings.First(pm => pm.Property == invFk.ForeignKeyProperty);
                var childFkCol = $"{relAlias}.{_dialect.QuoteIdentifier(childFkMap.ColumnName)}";

                // root pk column
                if (rootMapping.KeyProperties.Count == 0)
                    throw new InvalidOperationException($"Root '{rootMapping.EntityType.Name}' has no key.");

                var rootPkProp = rootMapping.KeyProperties[0];
                var rootPkMap = rootMapping.PropertyMappings.First(pm => pm.Property == rootPkProp);
                var rootPkCol = $"{rootAlias}.{_dialect.QuoteIdentifier(rootPkMap.ColumnName)}";

                joinParts.Add($"LEFT JOIN {relTable} {_dialect.FormatTableAlias(relAlias)} ON {childFkCol} = {rootPkCol}");
            }
            AppendSelectColumns(selectParts, relAlias, relAlias, relMapping!);
            var relPk = relMapping!.KeyProperties.FirstOrDefault();
            if (relPk is not null)
            {
                var relPkMap = relMapping.PropertyMappings.First(pm => pm.Property == relPk);
                // this must match the column alias produced in AppendSelectColumns
                splitOnCols.Add($"{relAlias}__{relPkMap.Property.Name}");
            }
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