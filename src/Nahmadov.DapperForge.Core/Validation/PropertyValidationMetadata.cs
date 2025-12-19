using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Nahmadov.DapperForge.Core.Validation;

/// <summary>
/// Captures validation attributes for a specific entity property.
/// </summary>
internal sealed class PropertyValidationMetadata(PropertyInfo property)
{
    /// <summary>
    /// Gets the property being described.
    /// </summary>
    public PropertyInfo Property { get; } = property ?? throw new ArgumentNullException(nameof(property));

    /// <summary>
    /// Gets the [Required] attribute if defined.
    /// </summary>
    public RequiredAttribute? Required { get; } = property.GetCustomAttribute<RequiredAttribute>();

    /// <summary>
    /// Gets the [StringLength] attribute if defined.
    /// </summary>
    public StringLengthAttribute? StringLength { get; } = property.GetCustomAttribute<StringLengthAttribute>();

    /// <summary>
    /// Gets the [MaxLength] attribute if defined.
    /// </summary>
    public MaxLengthAttribute? MaxLength { get; } = property.GetCustomAttribute<MaxLengthAttribute>();

    /// <summary>
    /// Indicates whether any validation attributes are present.
    /// </summary>
    public bool HasAnyRule =>
          Required is not null || StringLength is not null || MaxLength is not null;
}
