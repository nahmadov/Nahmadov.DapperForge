using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

using Nahmadov.DapperForge.Core.Modeling.Attributes;
using ForgeForeignKeyAttribute = Nahmadov.DapperForge.Core.Modeling.Attributes.ForeignKeyAttribute;

namespace Nahmadov.DapperForge.Core.Modeling.Mapping;
/// <summary>
/// Immutable snapshot of reflection-based metadata for an entity type.
/// Captures attributes and property lists once to avoid repeated discovery.
/// </summary>
internal sealed class EntityMetadataSnapshot(
    Type entityType,
    string tableName,
    string? schema,
    bool isReadOnly,
    IReadOnlyList<PropertyInfo> allProperties,
    IReadOnlyList<PropertyInfo> scalarProperties,
    IReadOnlyList<PropertyInfo> keyAttributeProperties,
    IReadOnlyDictionary<PropertyInfo, PropertyAttributeSnapshot> propertyAttributes,
    IReadOnlyDictionary<PropertyInfo, ForgeForeignKeyAttribute> foreignKeyAttributes)
{
    public Type EntityType { get; } = entityType;
    public string TableName { get; } = tableName;
    public string? Schema { get; } = schema;
    public bool IsReadOnly { get; } = isReadOnly;
    public IReadOnlyList<PropertyInfo> AllProperties { get; } = allProperties;
    public IReadOnlyList<PropertyInfo> ScalarProperties { get; } = scalarProperties;
    public IReadOnlyList<PropertyInfo> KeyAttributeProperties { get; } = keyAttributeProperties;
    public IReadOnlyDictionary<PropertyInfo, PropertyAttributeSnapshot> PropertyAttributes { get; } = propertyAttributes;
    public IReadOnlyDictionary<PropertyInfo, ForgeForeignKeyAttribute> ForeignKeyAttributes { get; } = foreignKeyAttributes;
}

/// <summary>
/// Captures attribute-derived metadata for a single property.
/// </summary>
internal sealed class PropertyAttributeSnapshot(
    string? columnName,
    DatabaseGeneratedOption? generatedOption,
    bool hasRequiredAttribute,
    int? maxLength)
{
    public string? ColumnName { get; } = columnName;
    public DatabaseGeneratedOption? GeneratedOption { get; } = generatedOption;
    public bool HasRequiredAttribute { get; } = hasRequiredAttribute;
    public int? MaxLength { get; } = maxLength;
}


