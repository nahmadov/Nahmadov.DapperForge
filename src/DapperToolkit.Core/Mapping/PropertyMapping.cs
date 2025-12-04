using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace DapperToolkit.Core.Mapping;

public sealed class PropertyMapping(
    PropertyInfo prop,
    ColumnAttribute? colAttr,
    DatabaseGeneratedAttribute? genAttr,
    bool isReadOnly = false,
    bool isRequired = false,
    int? maxLength = null,
    string? sequenceName = null)
{
    public PropertyInfo Property { get; } = prop ?? throw new ArgumentNullException(nameof(prop));
    public string ColumnName { get; } = colAttr?.Name ?? prop.Name;
    public DatabaseGeneratedOption? GeneratedOption { get; } = genAttr?.DatabaseGeneratedOption;
    public bool IsReadOnly { get; } = isReadOnly;
    public bool IsRequired { get; } = isRequired;
    public int? MaxLength { get; } = maxLength;
    public string? SequenceName { get; } = sequenceName;

    public bool IsIdentity => GeneratedOption == DatabaseGeneratedOption.Identity;
    public bool IsComputed => GeneratedOption == DatabaseGeneratedOption.Computed;
    public bool IsGenerated => IsIdentity || IsComputed || IsReadOnly;
    public bool UsesSequence => !string.IsNullOrWhiteSpace(SequenceName);
}
