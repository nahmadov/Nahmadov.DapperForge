using System.Reflection;

namespace DapperToolkit.Core.Validation;

/// <summary>
/// Builds and caches validation metadata for an entity type.
/// </summary>
internal static class EntityValidationMetadata<TEntity> where TEntity : class
{
    /// <summary>
    /// Validation metadata for each property that has validation rules.
    /// </summary>
    public static readonly PropertyValidationMetadata[] Properties = Build();

    /// <summary>
    /// Scans the entity type for validation attributes and builds metadata.
    /// </summary>
    /// <returns>Array of property validation metadata.</returns>
    private static PropertyValidationMetadata[] Build()
    {
        var type = typeof(TEntity);

        var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Where(p => p.CanRead &&
                                    p.GetIndexParameters().Length == 0)
                        .Select(p => new PropertyValidationMetadata(p))
                        .Where(m => m.HasAnyRule)
                        .ToArray();

        return props;
    }
}
