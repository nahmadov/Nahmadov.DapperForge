using System.Reflection;

namespace DapperToolkit.Core.Mapping;

public class EntityMapping(
    Type entityType,
    string tableName,
    string? schema,
    PropertyInfo? keyProperty,
    IReadOnlyList<PropertyInfo> properties,
    IReadOnlyList<PropertyMapping> propertyMappings,
    bool isReadOnly)
{
    public Type EntityType { get; } = entityType;
    public string TableName { get; } = tableName;
    public string? Schema { get; } = schema;
    public bool IsReadOnly { get; } = isReadOnly;
    public PropertyInfo? KeyProperty { get; } = keyProperty;
    public IReadOnlyList<PropertyInfo> Properties { get; } = properties;
    public IReadOnlyList<PropertyMapping> PropertyMappings { get; } = propertyMappings;
}
