using System;

namespace Nahmadov.DapperForge.Core.Attributes;

/// <summary>
/// Marks a property as a foreign key reference to another entity.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ForeignKeyAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the navigation property on this entity.
    /// </summary>
    public string NavigationPropertyName { get; }

    /// <summary>
    /// Gets the type of the related entity.
    /// </summary>
    public Type PrincipalEntityType { get; }

    /// <summary>
    /// Gets the name of the primary key property in the related entity.
    /// </summary>
    public string? PrincipalKeyPropertyName { get; }

    /// <summary>
    /// Initializes a new foreign key attribute.
    /// </summary>
    /// <param name="navigationPropertyName">Name of the navigation property.</param>
    /// <param name="principalEntityType">Type of the related entity.</param>
    /// <param name="principalKeyPropertyName">Name of the key property in the related entity (defaults to "Id").</param>
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
