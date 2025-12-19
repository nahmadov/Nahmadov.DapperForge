using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace Nahmadov.DapperForge.Core.Mapping;

/// <summary>
/// Represents mapping metadata for an entity property, including column name and generation behavior.
/// </summary>
public sealed class PropertyMapping(
    PropertyInfo prop,
    string columnName,
    DatabaseGeneratedOption? generatedOption,
    bool isReadOnly = false,
    bool isRequired = false,
    int? maxLength = null,
    string? sequenceName = null)
{
    /// <summary>
    /// Gets the underlying CLR property.
    /// </summary>
    public PropertyInfo Property { get; } = prop ?? throw new ArgumentNullException(nameof(prop));

    /// <summary>
    /// Gets the database column name for the property.
    /// </summary>
    public string ColumnName { get; } = string.IsNullOrWhiteSpace(columnName) ? prop.Name : columnName;

    /// <summary>
    /// Gets the generation strategy applied by the database, if any.
    /// </summary>
    public DatabaseGeneratedOption? GeneratedOption { get; } = generatedOption;

    /// <summary>
    /// Indicates whether the property is treated as read-only.
    /// </summary>
    public bool IsReadOnly { get; } = isReadOnly;

    /// <summary>
    /// Indicates whether the property is required during validation.
    /// </summary>
    public bool IsRequired { get; } = isRequired;

    /// <summary>
    /// Gets the configured maximum length for string properties, if set.
    /// </summary>
    public int? MaxLength { get; } = maxLength;

    /// <summary>
    /// Gets the name of the sequence used to generate values, if applicable.
    /// </summary>
    public string? SequenceName { get; } = sequenceName;

    /// <summary>
    /// True when the column is database-generated as an identity.
    /// </summary>
    public bool IsIdentity => GeneratedOption == DatabaseGeneratedOption.Identity;

    /// <summary>
    /// True when the column value is computed by the database.
    /// </summary>
    public bool IsComputed => GeneratedOption == DatabaseGeneratedOption.Computed;

    /// <summary>
    /// True when the mapping uses a sequence to generate values.
    /// </summary>
    public bool UsesSequence => !string.IsNullOrWhiteSpace(SequenceName);

    /// <summary>
    /// True when the column should not be provided on insert/update due to generation rules or read-only status.
    /// </summary>
    public bool IsGenerated => IsIdentity || IsComputed || IsReadOnly || UsesSequence;
}
