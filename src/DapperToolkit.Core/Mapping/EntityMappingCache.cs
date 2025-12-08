using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using DapperToolkit.Core.Attributes;

namespace DapperToolkit.Core.Mapping;

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

        var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Where(p =>
                            p.CanRead &&
                            p.CanWrite &&
                            p.GetIndexParameters().Length == 0 &&
                            p.GetCustomAttribute<NotMappedAttribute>() is null)
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

        return new EntityMapping(type, tableName, schema, keyProps, props, propertyMappings, isReadOnly);
    }

    /// <summary>
    /// Normalizes a key name by removing non-alphanumeric characters and upper-casing for comparisons.
    /// </summary>
    /// <param name="name">Key name to normalize.</param>
    /// <returns>Normalized key string.</returns>
    private static string NormalizeKeyName(string name)
        => new(name.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
