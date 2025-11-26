using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace DapperToolkit.Core.Validation;

internal sealed class PropertyValidationMetadata(PropertyInfo property)
{
    public PropertyInfo Property { get; } = property ?? throw new ArgumentNullException(nameof(property));
    public RequiredAttribute? Required { get; } = property.GetCustomAttribute<RequiredAttribute>();
    public StringLengthAttribute? StringLength { get; } = property.GetCustomAttribute<StringLengthAttribute>();

    public bool HasAnyRule =>
          Required is not null || StringLength is not null;
}