namespace DapperToolkit.Core.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class ForeignKeyNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}