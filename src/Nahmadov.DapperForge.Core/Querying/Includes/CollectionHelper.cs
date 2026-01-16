using System.Collections;
using System.Reflection;

namespace Nahmadov.DapperForge.Core.Querying.Includes;
/// <summary>
/// Helper class for creating and manipulating collections in Include operations.
/// </summary>
internal static class CollectionHelper
{
    /// <summary>
    /// Creates an empty collection instance for the specified property type.
    /// </summary>
    public static object CreateCollection(Type propertyType, Type elementType)
    {
        if (propertyType.IsInterface)
        {
            return CreateListInstance(elementType);
        }

        var instance = Activator.CreateInstance(propertyType);
        if (instance is not null)
            return instance;

        return CreateListInstance(elementType);
    }

    /// <summary>
    /// Creates a collection and populates it with the given items.
    /// </summary>
    public static object CreateCollectionWithItems(Type propertyType, Type elementType, IEnumerable<object> items)
    {
        var collection = CreateCollection(propertyType, elementType);

        foreach (var item in items)
        {
            AddToCollection(collection, item);
        }

        return collection;
    }

    /// <summary>
    /// Adds an item to a collection.
    /// </summary>
    public static void AddToCollection(object collection, object item)
    {
        if (collection is IList list)
        {
            list.Add(item);
            return;
        }

        var addMethod = collection.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
        if (addMethod is not null)
        {
            addMethod.Invoke(collection, [item]);
            return;
        }

        throw new NotSupportedException(
            $"Collection type '{collection.GetType().Name}' does not support Add().");
    }

    /// <summary>
    /// Gets the element type from a collection type.
    /// </summary>
    public static Type GetElementType(Type collectionType, string propertyName)
    {
        if (collectionType.IsGenericType && collectionType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return collectionType.GetGenericArguments()[0];
        }

        var enumerableInterface = collectionType
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerableInterface is not null)
        {
            return enumerableInterface.GetGenericArguments()[0];
        }

        throw new NotSupportedException(
            $"Navigation '{propertyName}' looks like a collection but element type could not be resolved.");
    }

    /// <summary>
    /// Determines if the given type represents a collection (excluding string).
    /// </summary>
    public static bool IsCollectionType(Type type)
    {
        return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
    }

    private static object CreateListInstance(Type elementType)
    {
        var listType = typeof(List<>).MakeGenericType(elementType);
        return Activator.CreateInstance(listType)
               ?? throw new InvalidOperationException($"Cannot create List<{elementType.Name}>.");
    }
}

