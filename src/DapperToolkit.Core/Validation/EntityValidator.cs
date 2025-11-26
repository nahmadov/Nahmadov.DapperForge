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

        // Read-only entity-ləri ümumiyyətlə buraya salmamaq da olar,
        // amma salınıbsa belə: insert/update onsuz da qadağandır.
        if (mapping.IsReadOnly)
        {
            throw new InvalidOperationException(
                $"Entity '{mapping.EntityType.Name}' is marked as ReadOnly and cannot be modified.");
        }

        var errors = new List<string>();

        foreach (var meta in EntityValidationMetadata<TEntity>.Properties)
        {
            var prop = meta.Property;
            var value = prop.GetValue(entity);

            // 1) [Required]
            if (meta.Required is not null)
            {
                if (value is null)
                {
                    errors.Add($"Property '{prop.Name}' is required.");
                }
                else if (value is string s && string.IsNullOrWhiteSpace(s))
                {
                    errors.Add($"Property '{prop.Name}' is required and cannot be empty.");
                }
            }

            // 2) [StringLength]
            if (meta.StringLength is not null && value is string str)
            {
                var attr = meta.StringLength;

                if (attr.MaximumLength > 0 && str.Length > attr.MaximumLength)
                {
                    errors.Add($"Property '{prop.Name}' exceeds maximum length of {attr.MaximumLength}.");
                }

                if (attr.MinimumLength > 0 && str.Length < attr.MinimumLength)
                {
                    errors.Add($"Property '{prop.Name}' is shorter than minimum length of {attr.MinimumLength}.");
                }
            }
        }

        if (errors.Count > 0)
        {
            var message =
                $"Validation failed for entity '{typeof(TEntity).Name}':{Environment.NewLine} - " +
                string.Join(Environment.NewLine + " - ", errors);

            // istəsən burada ValidationException at:
            throw new ValidationException(message);
        }
    }
}