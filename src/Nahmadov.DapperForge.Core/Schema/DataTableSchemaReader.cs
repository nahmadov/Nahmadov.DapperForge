using System.Data;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Schema;

namespace Nahmadov.DapperForge.Core.Schema;

/// <summary>
/// Translates the schema of an ADO.NET <see cref="DataTable"/> into temp-table columns on an
/// <see cref="ITempTableBuilder"/>, reusing the column-type inference rules.
/// </summary>
internal static class DataTableSchemaReader
{
    /// <summary>
    /// Adds one column to <paramref name="builder"/> for each <see cref="DataColumn"/> in
    /// <paramref name="schema"/>, inferring the SQL type from <see cref="DataColumn.DataType"/>,
    /// nullability from <see cref="DataColumn.AllowDBNull"/>, and string length from
    /// <see cref="DataColumn.MaxLength"/>.
    /// </summary>
    public static ITempTableBuilder Populate(ITempTableBuilder builder, DataTable schema)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(schema);

        if (schema.Columns.Count == 0)
            throw new ArgumentException(
                $"DataTable '{schema.TableName}' has no columns to build a temp table from.", nameof(schema));

        foreach (DataColumn column in schema.Columns)
        {
            var maxLength = column.DataType == typeof(string) && column.MaxLength > 0
                ? column.MaxLength
                : (int?)null;

            var (type, facets) = SqlColumnTypeInference.Infer(column.DataType, maxLength);

            // DataColumn.DataType is never Nullable<T>; nullability is carried by AllowDBNull.
            builder.Column(column.ColumnName, type, facets with { IsNullable = column.AllowDBNull });
        }

        return builder;
    }
}
