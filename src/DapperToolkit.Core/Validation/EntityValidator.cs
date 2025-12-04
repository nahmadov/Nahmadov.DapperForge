using System.ComponentModel.DataAnnotations;

using DapperToolkit.Core.Mapping;

namespace DapperToolkit.Core.Validation;

internal static class EntityValidator<TEntity> where TEntity : class
{
    public static void ValidateForInsert(TEntity entity, EntityMapping mapping)
        => Validate(entity, mapping, isInsert: true);

    public static void ValidateForUpdate(TEntity entity, EntityMapping mapping)
        => Validate(entity, mapping, isInsert: false);

    private static void Validate(TEntity entity, EntityMapping mapping, bool isInsert)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(mapping);

        if (mapping.IsReadOnly)
        {
            throw new InvalidOperationException(
                $"Entity '{mapping.EntityType.Name}' is marked as ReadOnly and cannot be modified.");
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
            var message =
                $"Validation failed for entity '{typeof(TEntity).Name}':{Environment.NewLine} - " +
                string.Join(Environment.NewLine + " - ", errors);

            throw new ValidationException(message);
        }
    }
}
