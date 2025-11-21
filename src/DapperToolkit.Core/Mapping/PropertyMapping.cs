using System.Reflection;

namespace DapperToolkit.Core.Mapping;

public sealed class PropertyMapping(PropertyInfo property, string columnName)
{
    public PropertyInfo Property { get; } = property;
    public string ColumnName { get; } = columnName;
}