using System.Collections.Concurrent;

using Nahmadov.DapperForge.Core.Mapping;

namespace Nahmadov.DapperForge.Core.Validation;

/// <summary>
/// Builds and caches validation metadata for an entity type.
/// </summary>
internal static class EntityValidationMetadata<TEntity> where TEntity : class
{
    private static readonly ConcurrentDictionary<Type, ValidationMetadataSet> _cache = new();

    /// <summary>
    /// Gets validation metadata for properties present in the mapping.
    /// </summary>
    /// <param name="mapping">Entity mapping to align validation with.</param>
    /// <returns>Cached validation metadata set.</returns>
    public static ValidationMetadataSet Get(EntityMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        return _cache.GetOrAdd(mapping.EntityType, _ =>
        {
            var props = mapping.PropertyMappings
                               .Select(pm => new PropertyValidationMetadata(pm.Property))
                               .Where(m => m.HasAnyRule)
                               .ToArray();

            var lookup = props.ToDictionary(m => m.Property, m => m);
            return new ValidationMetadataSet(props, lookup);
        });
    }
}

internal sealed class ValidationMetadataSet(
    PropertyValidationMetadata[] properties,
    IReadOnlyDictionary<System.Reflection.PropertyInfo, PropertyValidationMetadata> lookup)
{
    public PropertyValidationMetadata[] Properties { get; } = properties;
    public IReadOnlyDictionary<System.Reflection.PropertyInfo, PropertyValidationMetadata> Lookup { get; } = lookup;
}
