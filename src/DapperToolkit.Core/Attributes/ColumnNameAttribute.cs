namespace DapperToolkit.Core.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class ColumnNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}