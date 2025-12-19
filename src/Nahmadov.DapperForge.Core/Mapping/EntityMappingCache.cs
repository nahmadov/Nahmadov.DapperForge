using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

using Nahmadov.DapperForge.Core.Attributes;
using ForgeForeignKeyAttribute = Nahmadov.DapperForge.Core.Attributes.ForeignKeyAttribute;

namespace Nahmadov.DapperForge.Core.Mapping;

/// <summary>
/// Builds and caches attribute-based entity metadata to reduce reflection overhead.
/// </summary>
internal static class EntityMappingCache<TEntity>
    where TEntity : class
{
    /// <summary>
    /// Cached reflection snapshot for the entity type.
    /// </summary>
    public static readonly EntityMetadataSnapshot Snapshot = BuildSnapshot();

    /// <summary>
    /// Creates a metadata snapshot using attributes and conventions.
    /// </summary>
    /// <returns>An <see cref="EntityMetadataSnapshot"/> capturing reflection data.</returns>
    private static EntityMetadataSnapshot BuildSnapshot()
    {
        var type = typeof(TEntity);

        var tableAttr = type.GetCustomAttribute<TableAttribute>();
        var tableName = tableAttr?.Name ?? type.Name;
        string? schema = tableAttr?.Schema;

        // Get all properties (including navigation properties)
        var allProps = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                           .Where(p =>
                               p.CanRead &&
                               p.CanWrite &&
                               p.GetIndexParameters().Length == 0 &&
                               p.GetCustomAttribute<NotMappedAttribute>() is null)
                           .ToArray();

        // Get only scalar properties for column mapping
        var props = allProps
                    .Where(p => IsScalarProperty(p.PropertyType))
                    .ToArray();

        if (props.Length == 0)
            throw new InvalidOperationException($"Type {type.Name} has no writable public properties.");

        var isReadOnly = type.GetCustomAttribute<ReadOnlyEntityAttribute>() is not null;

        var keyProps = props
            .Where(p => p.GetCustomAttribute<KeyAttribute>() is not null)
            .ToArray();

        var propertyAttributes = new Dictionary<PropertyInfo, PropertyAttributeSnapshot>();
        foreach (var prop in props)
        {
            var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
            var genAttr = prop.GetCustomAttribute<DatabaseGeneratedAttribute>();
            var hasRequired = prop.GetCustomAttribute<RequiredAttribute>() is not null
                              || prop.GetCustomAttribute<KeyAttribute>() is not null;

            var stringLength = prop.GetCustomAttribute<StringLengthAttribute>();
            var maxLengthAttr = prop.GetCustomAttribute<MaxLengthAttribute>();
            var maxLength = stringLength?.MaximumLength > 0
                ? stringLength.MaximumLength
                : maxLengthAttr?.Length > 0 ? maxLengthAttr.Length : (int?)null;

            propertyAttributes[prop] = new PropertyAttributeSnapshot(colAttr, genAttr, hasRequired, maxLength);
        }

        var foreignKeyAttributes = allProps
            .Select(p => (Property: p, Attr: p.GetCustomAttribute<ForgeForeignKeyAttribute>()))
            .Where(t => t.Attr is not null)
            .ToDictionary(t => t.Property, t => t.Attr!);

        return new EntityMetadataSnapshot(
            type,
            tableName,
            schema,
            isReadOnly,
            allProps,
            props,
            keyProps,
            propertyAttributes,
            foreignKeyAttributes);
    }

    private static bool IsScalarProperty(Type propertyType)
    {
        // Check if the type is a scalar (simple) type that can be mapped to a database column
        // Exclude collection types (List<T>, IEnumerable<T>) and other complex navigation properties
        if (propertyType.IsGenericType)
        {
            var genericDef = propertyType.GetGenericTypeDefinition();
            if (genericDef == typeof(List<>) ||
                genericDef == typeof(IEnumerable<>) ||
                genericDef == typeof(ICollection<>) ||
                genericDef == typeof(IList<>) ||
                genericDef == typeof(ISet<>))
            {
                return false;
            }
        }

        // Check if it's a nullable value type
        if (Nullable.GetUnderlyingType(propertyType) is { } underlyingType)
        {
            return IsScalarProperty(underlyingType);
        }

        // Check if the type is a scalar type (primitive, string, DateTime, Guid, etc.)
        var isScalar = propertyType.IsValueType ||
                       propertyType == typeof(string) ||
                       propertyType == typeof(byte[]) ||
                       propertyType == typeof(object);

        // If it's not a scalar type, it's a reference type (navigation property) - exclude it
        if (!isScalar && !propertyType.IsValueType)
        {
            return false;
        }

        return isScalar;
    }
}
