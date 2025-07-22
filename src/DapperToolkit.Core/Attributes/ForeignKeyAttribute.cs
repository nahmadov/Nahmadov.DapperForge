namespace DapperToolkit.Core.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class ForeignKeyAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}