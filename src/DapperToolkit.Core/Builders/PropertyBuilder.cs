using DapperToolkit.Core.Mapping;

namespace DapperToolkit.Core.Builders;

public class PropertyBuilder(PropertyConfig property)
{
    private readonly PropertyConfig _property = property;

    public PropertyBuilder HasColumnName(string columnName)
    {
        _property.SetColumnName(columnName);
        return this;
    }

    public PropertyBuilder IsRequired()
    {
        _property.SetRequired(true);
        return this;
    }

    public PropertyBuilder HasMaxLength(int maxLength)
    {
        _property.SetMaxLength(maxLength);
        return this;
    }

    public PropertyBuilder IsReadOnly()
    {
        _property.SetReadOnly(true);
        return this;
    }
}