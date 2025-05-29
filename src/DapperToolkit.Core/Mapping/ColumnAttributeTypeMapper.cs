using System.Reflection;

using Dapper;

using DapperToolkit.Core.Attributes;

namespace DapperToolkit.Core.Mapping;

public class ColumnAttributeTypeMapper<T> : FallbackTypeMapper
{
    public ColumnAttributeTypeMapper() : base([new CustomPropertyTypeMap(typeof(T), (type, columnName) => SelectProperty(type, columnName)!), new DefaultTypeMap(typeof(T))]) { }

    private static PropertyInfo? SelectProperty(Type type, string columnName)
    {
        return type.GetProperties()
            .FirstOrDefault(prop =>
            {
                var attr = prop.GetCustomAttribute<ColumnNameAttribute>();
                if (attr != null)
                    return string.Equals(attr.Name, columnName, StringComparison.OrdinalIgnoreCase);

                return string.Equals(prop.Name, columnName, StringComparison.OrdinalIgnoreCase);
            });
    }
}