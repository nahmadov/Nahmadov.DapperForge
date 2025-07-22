namespace DapperToolkit.Core.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class InversePropertyAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}