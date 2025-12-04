namespace DapperToolkit.Core.Mapping;

public class PropertyConfig(string propertyName)
{
    public string PropertyName { get; } = propertyName;
    public string ColumnName { get; private set; } = propertyName;
    public bool IsRequired { get; private set; }
    public bool IsReadOnly { get; private set; }
    public int? MaxLength { get; private set; }

    public void SetColumnName(string columnName) => ColumnName = columnName;
    public void SetRequired(bool required) => IsRequired = required;
    public void SetReadOnly(bool readOnly) => IsReadOnly = readOnly;
    public void SetMaxLength(int? length) => MaxLength = length;
}