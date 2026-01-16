using System.Reflection;

namespace Nahmadov.DapperForge.Core.Modeling.Mapping;
/// <summary>
/// Represents an immutable mapping between an entity type and its database representation.
/// </summary>
/// <remarks>
/// Supports Primary Keys and Alternate Keys (business keys) for flexible entity identification.
/// </remarks>
public class EntityMapping(
    Type entityType,
    string tableName,
    string? schema,
    IReadOnlyList<PropertyInfo> keyProperties,
    IReadOnlyList<PropertyInfo> properties,
    IReadOnlyList<PropertyMapping> propertyMappings,
    bool isReadOnly,
    IReadOnlyList<ForeignKeyMapping>? foreignKeys = null,
    IReadOnlyList<PropertyInfo>? alternateKeyProperties = null)
{
    public Type EntityType { get; } = entityType;
    public string TableName { get; } = tableName;
    public string? Schema { get; } = schema;
    public bool IsReadOnly { get; } = isReadOnly;

    /// <summary>
    /// Primary key properties. Empty if entity has no primary key.
    /// </summary>
    public IReadOnlyList<PropertyInfo> KeyProperties { get; } = keyProperties;

    /// <summary>
    /// First primary key property if exists, otherwise null.
    /// </summary>
    public PropertyInfo? KeyProperty { get; } = keyProperties.FirstOrDefault();

    /// <summary>
    /// Alternate key (business key) properties. Used when primary key doesn't exist.
    /// Alternate keys represent business-level uniqueness (e.g., employee number, email).
    /// </summary>
    public IReadOnlyList<PropertyInfo> AlternateKeyProperties { get; } = alternateKeyProperties ?? [];

    /// <summary>
    /// First alternate key property if exists, otherwise null.
    /// </summary>
    public PropertyInfo? AlternateKeyProperty { get; } = alternateKeyProperties?.FirstOrDefault();

    /// <summary>
    /// Indicates whether this entity has a primary key defined.
    /// </summary>
    public bool HasPrimaryKey => KeyProperties.Count > 0;

    /// <summary>
    /// Indicates whether this entity has an alternate key defined.
    /// </summary>
    public bool HasAlternateKey => AlternateKeyProperties.Count > 0;

    /// <summary>
    /// Gets the effective key to use for operations. Returns primary key if available,
    /// otherwise returns alternate key. Returns empty list if neither exists.
    /// </summary>
    public IReadOnlyList<PropertyInfo> EffectiveKey => HasPrimaryKey ? KeyProperties : AlternateKeyProperties;

    public IReadOnlyList<PropertyInfo> Properties { get; } = properties;
    public IReadOnlyList<PropertyMapping> PropertyMappings { get; } = propertyMappings;
    public IReadOnlyList<ForeignKeyMapping> ForeignKeys { get; } = foreignKeys ?? [];
}

