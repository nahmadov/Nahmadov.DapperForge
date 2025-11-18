using System.Reflection;
using DapperToolkit.Core.Attributes;

namespace DapperToolkit.Core.Common;

public static class PrimaryKeyHelper
{

    public static PropertyInfo? GetPrimaryKeyProperty(Type entityType)
    {
        var primaryKeyProperty = entityType.GetProperties()
            .FirstOrDefault(p => p.GetCustomAttribute<PrimaryKeyAttribute>() != null);

        if (primaryKeyProperty != null)
            return primaryKeyProperty;

        return entityType.GetProperty("Id");
    }


    public static string? GetPrimaryKeyPropertyName(Type entityType)
    {
        return GetPrimaryKeyProperty(entityType)?.Name;
    }


    public static string? GetPrimaryKeyColumnName(Type entityType)
    {
        var primaryKeyProperty = GetPrimaryKeyProperty(entityType);
        if (primaryKeyProperty == null)
            return null;

        var primaryKeyAttr = primaryKeyProperty.GetCustomAttribute<PrimaryKeyAttribute>();
        if (primaryKeyAttr?.Name != null)
            return primaryKeyAttr.Name;

        var columnAttr = primaryKeyProperty.GetCustomAttribute<ColumnNameAttribute>();
        if (columnAttr?.Name != null)
            return columnAttr.Name;

        return primaryKeyProperty.Name;
    }


    public static object? GetPrimaryKeyValue(object entity)
    {
        if (entity == null)
            return null;

        var primaryKeyProperty = GetPrimaryKeyProperty(entity.GetType());
        return primaryKeyProperty?.GetValue(entity);
    }


    public static IEnumerable<PropertyInfo> GetNonPrimaryKeyProperties(Type entityType)
    {
        var primaryKeyProperty = GetPrimaryKeyProperty(entityType);
        return entityType.GetProperties()
            .Where(p => p != primaryKeyProperty);
    }


    public static void ValidatePrimaryKey(Type entityType, string operationName)
    {
        var primaryKeyProperty = GetPrimaryKeyProperty(entityType);
        if (primaryKeyProperty == null)
        {
            throw ValidationHelper.CreateEntityOperationException(entityType, operationName, 
                "Entity must have a primary key property (either marked with [PrimaryKey] attribute or named 'Id')");
        }
    }


    public static void ValidatePrimaryKeyValue(object entity, string operationName)
    {
        ValidationHelper.ValidateNotNull(entity, nameof(entity));

        var primaryKeyValue = GetPrimaryKeyValue(entity);
        if (primaryKeyValue == null)
        {
            throw ValidationHelper.CreateEntityOperationException(entity.GetType(), operationName,
                "Primary key value cannot be null");
        }
    }
}
