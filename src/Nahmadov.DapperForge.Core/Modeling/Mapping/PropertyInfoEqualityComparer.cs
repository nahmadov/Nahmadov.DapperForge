using System.Reflection;

namespace Nahmadov.DapperForge.Core.Modeling.Mapping;

/// <summary>
/// Compares PropertyInfo instances by name and declaring type hierarchy.
/// Handles inheritance scenarios where the same logical property may have different PropertyInfo references
/// (e.g., when a property is declared in a base class but accessed through a derived class expression).
/// </summary>
internal sealed class PropertyInfoEqualityComparer : IEqualityComparer<PropertyInfo>
{
    /// <summary>
    /// Singleton instance of the comparer.
    /// </summary>
    public static readonly PropertyInfoEqualityComparer Instance = new();

    private PropertyInfoEqualityComparer() { }

    /// <summary>
    /// Determines whether two PropertyInfo instances represent the same property,
    /// accounting for inheritance hierarchies.
    /// </summary>
    public bool Equals(PropertyInfo? x, PropertyInfo? y)
    {
        if (ReferenceEquals(x, y))
            return true;

        if (x is null || y is null)
            return false;

        // Same name is required
        if (!string.Equals(x.Name, y.Name, StringComparison.Ordinal))
            return false;

        // Check if they belong to the same type hierarchy
        var xDeclaring = x.DeclaringType;
        var yDeclaring = y.DeclaringType;

        if (xDeclaring is null || yDeclaring is null)
            return false;

        // Same declaring type
        if (xDeclaring == yDeclaring)
            return true;

        // One is assignable from the other (inheritance relationship)
        if (xDeclaring.IsAssignableFrom(yDeclaring) || yDeclaring.IsAssignableFrom(xDeclaring))
            return true;

        // Check if both types implement the same interface that declares this property
        return SharesPropertyThroughInterface(x, y);
    }

    /// <summary>
    /// Returns a hash code based on the property name only,
    /// since declaring types may differ in inheritance scenarios.
    /// </summary>
    public int GetHashCode(PropertyInfo obj)
    {
        return obj.Name.GetHashCode(StringComparison.Ordinal);
    }

    /// <summary>
    /// Checks if two properties share the same declaration through an interface.
    /// </summary>
    private static bool SharesPropertyThroughInterface(PropertyInfo x, PropertyInfo y)
    {
        var xDeclaring = x.DeclaringType!;
        var yDeclaring = y.DeclaringType!;

        // Get all interfaces that declare a property with this name
        var xInterfaces = xDeclaring.GetInterfaces()
            .Where(i => i.GetProperty(x.Name) is not null);

        var yInterfaces = yDeclaring.GetInterfaces()
            .Where(i => i.GetProperty(y.Name) is not null);

        // Check if any interface is shared or has inheritance relationship
        foreach (var xi in xInterfaces)
        {
            foreach (var yi in yInterfaces)
            {
                if (xi == yi || xi.IsAssignableFrom(yi) || yi.IsAssignableFrom(xi))
                    return true;
            }
        }

        return false;
    }
}
