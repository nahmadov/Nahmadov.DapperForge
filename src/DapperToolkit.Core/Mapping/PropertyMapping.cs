using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace DapperToolkit.Core.Mapping;

public sealed class PropertyMapping(PropertyInfo prop, ColumnAttribute? colAttr, DatabaseGeneratedAttribute? genAttr)
{
    public PropertyInfo Property { get; } = prop ?? throw new ArgumentNullException(nameof(prop));
    public string ColumnName { get; } = colAttr?.Name ?? prop.Name;
    public DatabaseGeneratedOption? GeneratedOption { get; } = genAttr?.DatabaseGeneratedOption;

    public bool IsIdentity => GeneratedOption == DatabaseGeneratedOption.Identity;
    public bool IsComputed => GeneratedOption == DatabaseGeneratedOption.Computed;
    public bool IsGenerated => IsIdentity || IsComputed;
}