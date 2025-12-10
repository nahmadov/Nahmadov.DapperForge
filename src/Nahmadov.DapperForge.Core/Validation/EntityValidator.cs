using Nahmadov.DapperForge.Core.Exceptions;
using Nahmadov.DapperForge.Core.Mapping;

namespace Nahmadov.DapperForge.Core.Validation;

/// <summary>
/// Performs validation for entities based on mapping and data annotations.
/// </summary>
internal static class EntityValidator<TEntity> where TEntity : class
{
    /// <summary>
    /// Validates an entity for insert operations.
    /// </summary>
    /// <param name="entity">Entity to validate.</param>
    /// <param name="mapping">Mapping metadata describing the entity.</param>
    public static void ValidateForInsert(TEntity entity, EntityMapping mapping)
        => Validate(entity, mapping, isInsert: true);

    /// <summary>
    /// Validates an entity for update operations.
    /// </summary>
    /// <param name="entity">Entity to validate.</param>
    /// <param name="mapping">Mapping metadata describing the entity.</param>
    public static void ValidateForUpdate(TEntity entity, EntityMapping mapping)
        => Validate(entity, mapping, isInsert: false);

    /// <summary>
    /// Validates the entity based on required fields and length constraints.
    /// </summary>
    /// <param name="entity">Entity instance to validate.</param>
    /// <param name="mapping">Mapping metadata describing the entity.</param>
    /// <param name="isInsert">Indicates whether the operation is an insert.</param>
    private static void Validate(TEntity entity, EntityMapping mapping, bool isInsert)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(mapping);

        if (mapping.IsReadOnly)
        {
            var operationType = isInsert ? OperationType.Insert : OperationType.Update;
            throw new DapperReadOnlyException(operationType, mapping.EntityType.Name);
        }

        var errors = new List<string>();
        var metaLookup = EntityValidationMetadata<TEntity>.Properties
            .ToDictionary(m => m.Property, m => m);

        foreach (var propMap in mapping.PropertyMappings)
        {
            var prop = propMap.Property;
            var value = prop.GetValue(entity);

            metaLookup.TryGetValue(prop, out var meta);

            var required = propMap.IsRequired || meta?.Required is not null;
            var stringLengthAttr = meta?.StringLength;
            var maxLength = propMap.MaxLength ??
                            (stringLengthAttr?.MaximumLength > 0 ? stringLengthAttr.MaximumLength : (int?)null);
            var minLength = stringLengthAttr?.MinimumLength > 0 ? stringLengthAttr.MinimumLength : (int?)null;

            if (required)
            {
                if (value is null)
                {
                    errors.Add($"Property '{prop.Name}' is required.");
                    continue;
                }
                if (value is string s && string.IsNullOrWhiteSpace(s))
                {
                    errors.Add($"Property '{prop.Name}' is required and cannot be empty.");
                    continue;
                }
            }

            if (value is string str)
            {
                if (maxLength is not null && str.Length > maxLength.Value)
                    errors.Add($"Property '{prop.Name}' exceeds maximum length of {maxLength.Value}.");

                if (minLength is not null && str.Length < minLength.Value)
                    errors.Add($"Property '{prop.Name}' is shorter than minimum length of {minLength.Value}.");
            }
        }

        if (errors.Count > 0)
        {
            throw new DapperValidationException(typeof(TEntity).Name, errors);
        }
    }
}
