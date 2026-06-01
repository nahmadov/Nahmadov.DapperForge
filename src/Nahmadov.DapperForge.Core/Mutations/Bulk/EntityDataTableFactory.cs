using System.Data;

using Nahmadov.DapperForge.Core.Modeling.Mapping;

namespace Nahmadov.DapperForge.Core.Mutations.Bulk;

/// <summary>
/// Builds an ADO.NET <see cref="DataTable"/> from entities using their <see cref="EntityMapping"/>,
/// so bulk-copy column mappings stay consistent with the temp-table DDL produced by
/// <c>CreateTempTableLikeAsync</c> (same mapping source, same column selection).
/// </summary>
internal static class EntityDataTableFactory
{
    /// <summary>
    /// Creates a <see cref="DataTable"/> whose columns are the entity's mapped, writable columns
    /// (database-generated / identity columns excluded) named by their resolved column names.
    /// </summary>
    public static DataTable ToDataTable<TEntity>(EntityMapping mapping, IEnumerable<TEntity> rows)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(rows);

        var columns = mapping.PropertyMappings.Where(pm => !pm.IsGenerated).ToList();

        var table = new DataTable();
        foreach (var pm in columns)
            table.Columns.Add(new DataColumn(pm.ColumnName, ColumnClrType(pm)));

        foreach (var entity in rows)
        {
            var values = new object?[columns.Count];
            for (var i = 0; i < columns.Count; i++)
                values[i] = ToColumnValue(columns[i], entity);

            table.Rows.Add(values);
        }

        return table;
    }

    private static Type ColumnClrType(PropertyMapping pm)
    {
        var type = Nullable.GetUnderlyingType(pm.Property.PropertyType) ?? pm.Property.PropertyType;
        return type.IsEnum ? Enum.GetUnderlyingType(type) : type;
    }

    private static object ToColumnValue<TEntity>(PropertyMapping pm, TEntity entity)
    {
        var value = pm.Property.GetValue(entity);
        if (value is null)
            return DBNull.Value;

        var type = Nullable.GetUnderlyingType(pm.Property.PropertyType) ?? pm.Property.PropertyType;
        return type.IsEnum ? Convert.ChangeType(value, Enum.GetUnderlyingType(type)) : value;
    }
}
