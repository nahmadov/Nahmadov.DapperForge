using System;

namespace Nahmadov.DapperForge.Core.Attributes;

/// <summary>
/// Marks a property as a foreign key reference to another entity.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ForeignKeyAttribute : Attribute
{
    public string NavigationPropertyName { get; }

    public Type PrincipalEntityType { get; }

    public string? PrincipalKeyPropertyName { get; }

    public ForeignKeyAttribute(
        string navigationPropertyName,
        Type principalEntityType,
        string? principalKeyPropertyName = null)
    {
        if (string.IsNullOrWhiteSpace(navigationPropertyName))
            throw new ArgumentException("Navigation property name cannot be empty.", nameof(navigationPropertyName));

        NavigationPropertyName = navigationPropertyName;
        PrincipalEntityType = principalEntityType ?? throw new ArgumentNullException(nameof(principalEntityType));
        PrincipalKeyPropertyName = principalKeyPropertyName;
    }
}
