using System.Reflection;

namespace DapperToolkit.Core.Mapping;

public class EntityMapping(
    Type entityType,
    string tableName,
    string? schema,
    IReadOnlyList<PropertyInfo> keyProperties,
    IReadOnlyList<PropertyInfo> properties,
    IReadOnlyList<PropertyMapping> propertyMappings,
    bool isReadOnly)
{
    public Type EntityType { get; } = entityType;
    public string TableName { get; } = tableName;
    public string? Schema { get; } = schema;
    public bool IsReadOnly { get; } = isReadOnly;
    public IReadOnlyList<PropertyInfo> KeyProperties { get; } = keyProperties;
    public PropertyInfo? KeyProperty { get; } = keyProperties.FirstOrDefault();
    public IReadOnlyList<PropertyInfo> Properties { get; } = properties;
    public IReadOnlyList<PropertyMapping> PropertyMappings { get; } = propertyMappings;
}
