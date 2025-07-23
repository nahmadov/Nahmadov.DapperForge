namespace DapperToolkit.Core.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class PrimaryKeyAttribute(string? name = null) : Attribute
{
    public string? Name { get; set; } = name;
}