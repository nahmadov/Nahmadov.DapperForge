using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

using Nahmadov.DapperForge.Core.Attributes;

namespace Nahmadov.DapperForge.Core.Mapping;

/// <summary>
/// Builds and caches attribute-based entity mappings to reduce reflection overhead.
/// </summary>
internal static class EntityMappingCache<TEntity>
    where TEntity : class
{
    /// <summary>
    /// Cached mapping for the entity type.
    /// </summary>
    public static readonly EntityMapping Mapping = Build();

    /// <summary>
    /// Creates mapping metadata using attributes and conventions.
    /// </summary>
    /// <returns>An <see cref="EntityMapping"/> describing table, schema, keys, and properties.</returns>
    private static EntityMapping Build()
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
            .ToList();

        if (keyProps.Count == 0)
        {
            var single = props.FirstOrDefault(p => string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase))
                      ?? props.FirstOrDefault(p => string.Equals(p.Name, type.Name + "Id", StringComparison.OrdinalIgnoreCase))
                      ?? props.FirstOrDefault(p => NormalizeKeyName(p.Name) == NormalizeKeyName(type.Name + "Id"));

            if (single is not null)
                keyProps.Add(single);
        }

        if (keyProps.Count == 0 && !isReadOnly)
            throw new InvalidOperationException(
                $"Type {type.Name} has no key property. Define [Key] or an 'Id'/{type.Name}Id property.");

        var propertyMappings = props.Select(p =>
        {
            var colAttr = p.GetCustomAttribute<ColumnAttribute>();
            var genAttr = p.GetCustomAttribute<DatabaseGeneratedAttribute>();
            var required = p.GetCustomAttribute<KeyAttribute>() is not null
                           || p.GetCustomAttribute<RequiredAttribute>() is not null;

            var stringLength = p.GetCustomAttribute<StringLengthAttribute>();
            var maxLengthAttr = p.GetCustomAttribute<MaxLengthAttribute>();
            var maxLength = stringLength?.MaximumLength > 0
                ? stringLength.MaximumLength
                : maxLengthAttr?.Length > 0 ? maxLengthAttr.Length : (int?)null;

            if (genAttr is null && keyProps.Contains(p))
            {
                genAttr = new DatabaseGeneratedAttribute(DatabaseGeneratedOption.Identity);
            }

            return new PropertyMapping(p, colAttr, genAttr, false, required, maxLength);
        }).ToList();

        // Pass allProps to BuildForeignKeyMappings so it can find navigation properties
        var foreignKeys = BuildForeignKeyMappings(type, allProps, propertyMappings, keyProps);

        return new EntityMapping(type, tableName, schema, keyProps, props, propertyMappings, isReadOnly, foreignKeys);
    }

    /// <summary>
    /// Normalizes a key name by removing non-alphanumeric characters and upper-casing for comparisons.
    /// </summary>
    /// <param name="name">Key name to normalize.</param>
    /// <returns>Normalized key string.</returns>
    private static string NormalizeKeyName(string name)
        => new(name.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    /// <summary>
    /// Builds foreign key mappings from attributes on the entity's properties.
    /// </summary>
    private static IReadOnlyList<ForeignKeyMapping> BuildForeignKeyMappings(
        Type entityType,
        PropertyInfo[] properties,
        List<PropertyMapping> propertyMappings,
        List<PropertyInfo> keyProps)
    {
        var foreignKeys = new List<ForeignKeyMapping>();

        foreach (var prop in properties)
        {
            var fkAttr = prop.GetCustomAttribute<Attributes.ForeignKeyAttribute>();
            if (fkAttr is null)
                continue;

            // Find the navigation property
            var navProp = properties.FirstOrDefault(p =>
                string.Equals(p.Name, fkAttr.NavigationPropertyName, StringComparison.Ordinal)) ?? throw new InvalidOperationException(
                    $"Navigation property '{fkAttr.NavigationPropertyName}' not found on entity '{entityType.Name}'.");

            // Get the foreign key column name
            var fkColumnMapping = propertyMappings.FirstOrDefault(pm => pm.Property == prop);
            if (fkColumnMapping is null)
                throw new InvalidOperationException($"Property '{prop.Name}' has no column mapping.");

            var fkColumnName = fkColumnMapping.ColumnName;

            // Get the principal entity's key property name
            var principalKeyPropName = fkAttr.PrincipalKeyPropertyName ?? "Id";

            // Get the principal entity's table name and schema
            var principalType = fkAttr.PrincipalEntityType;
            var principalTableAttr = principalType.GetCustomAttribute<TableAttribute>();
            var principalTableName = principalTableAttr?.Name ?? principalType.Name;
            var principalSchema = principalTableAttr?.Schema;

            // Get the principal entity's key column name
            var principalKeyProp = principalType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(p => string.Equals(p.Name, principalKeyPropName, StringComparison.Ordinal));

            if (principalKeyProp is null)
                throw new InvalidOperationException(
                    $"Key property '{principalKeyPropName}' not found on entity '{principalType.Name}'.");

            var principalColumnAttr = principalKeyProp.GetCustomAttribute<ColumnAttribute>();
            var principalColumnName = principalColumnAttr?.Name ?? principalKeyPropName;

            var fkMapping = new ForeignKeyMapping(
                navProp,
                prop,
                principalType,
                fkColumnName,
                principalColumnName,
                principalTableName,
                principalSchema);

            foreignKeys.Add(fkMapping);
        }

        return foreignKeys;
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
