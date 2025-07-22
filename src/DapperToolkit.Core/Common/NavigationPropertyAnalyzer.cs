using System.Linq.Expressions;
using System.Reflection;
using DapperToolkit.Core.Attributes;

namespace DapperToolkit.Core.Common;

public class NavigationPropertyAnalyzer
{
    public static NavigationPropertyInfo AnalyzeIncludeExpression<T, TProperty>(Expression<Func<T, TProperty>> includeExpression)
    {
        if (includeExpression.Body is not MemberExpression memberExpression)
        {
            throw new ArgumentException("Include expression must be a property access expression.");
        }

        var property = memberExpression.Member as PropertyInfo;
        if (property == null)
        {
            throw new ArgumentException("Include expression must access a property.");
        }

        var sourceType = typeof(T);
        var targetType = GetTargetType(property);

        var foreignKeyInfo = GetForeignKeyInfo(sourceType, targetType, property);
        var joinInfo = GenerateJoinInfo(sourceType, targetType, foreignKeyInfo);

        return new NavigationPropertyInfo
        {
            PropertyName = property.Name,
            SourceType = sourceType,
            TargetType = targetType,
            ForeignKeyInfo = foreignKeyInfo,
            JoinInfo = joinInfo
        };
    }

    private static Type GetTargetType(PropertyInfo property)
    {
        var propertyType = property.PropertyType;
        
        if (propertyType.IsGenericType)
        {
            var genericTypeDefinition = propertyType.GetGenericTypeDefinition();
            if (typeof(IEnumerable<>).IsAssignableFrom(genericTypeDefinition) ||
                genericTypeDefinition == typeof(List<>) ||
                genericTypeDefinition == typeof(ICollection<>) ||
                genericTypeDefinition == typeof(IList<>))
            {
                return propertyType.GetGenericArguments()[0];
            }
        }

        return propertyType;
    }

    private static ForeignKeyInfo GetForeignKeyInfo(Type sourceType, Type targetType, PropertyInfo navigationProperty)
    {
        var foreignKeyAttr = navigationProperty.GetCustomAttribute<ForeignKeyAttribute>();
        if (foreignKeyAttr != null)
        {
            return new ForeignKeyInfo
            {
                ForeignKeyColumnName = foreignKeyAttr.Name,
                IsCollection = IsCollectionProperty(navigationProperty)
            };
        }

        var inversePropertyAttr = navigationProperty.GetCustomAttribute<InversePropertyAttribute>();
        if (inversePropertyAttr != null)
        {
            var inverseProperty = targetType.GetProperty(inversePropertyAttr.Name);
            if (inverseProperty != null)
            {
                var inverseForeignKeyAttr = inverseProperty.GetCustomAttribute<ForeignKeyAttribute>();
                if (inverseForeignKeyAttr != null)
                {
                    return new ForeignKeyInfo
                    {
                        ForeignKeyColumnName = inverseForeignKeyAttr.Name,
                        IsCollection = IsCollectionProperty(navigationProperty)
                    };
                }
                
                var foreignKeyProperty = targetType.GetProperties()
                    .FirstOrDefault(p => p.GetCustomAttribute<ForeignKeyAttribute>() != null);
                
                if (foreignKeyProperty != null)
                {
                    var fkAttr = foreignKeyProperty.GetCustomAttribute<ForeignKeyAttribute>();
                    return new ForeignKeyInfo
                    {
                        ForeignKeyColumnName = fkAttr!.Name,
                        IsCollection = IsCollectionProperty(navigationProperty)
                    };
                }
            }
        }

        var allForeignKeyProperties = sourceType.GetProperties()
            .Where(p => p.GetCustomAttribute<ForeignKeyAttribute>() != null)
            .ToList();

        foreach (var fkProperty in allForeignKeyProperties)
        {
            var fkAttr = fkProperty.GetCustomAttribute<ForeignKeyAttribute>();
            if (fkAttr != null)
            {
                return new ForeignKeyInfo
                {
                    ForeignKeyColumnName = fkAttr.Name,
                    IsCollection = IsCollectionProperty(navigationProperty)
                };
            }
        }

        var conventionalForeignKeyProperty = sourceType.GetProperties()
            .FirstOrDefault(p => p.Name.Equals($"{targetType.Name}Id", StringComparison.OrdinalIgnoreCase));

        if (conventionalForeignKeyProperty != null)
        {
            var columnAttr = conventionalForeignKeyProperty.GetCustomAttribute<ColumnNameAttribute>();
            return new ForeignKeyInfo
            {
                ForeignKeyColumnName = columnAttr?.Name ?? conventionalForeignKeyProperty.Name,
                IsCollection = IsCollectionProperty(navigationProperty)
            };
        }

        throw new InvalidOperationException($"Could not determine foreign key for navigation property '{navigationProperty.Name}' on type '{sourceType.Name}'.");
    }

    private static bool IsCollectionProperty(PropertyInfo property)
    {
        var propertyType = property.PropertyType;
        return propertyType.IsGenericType &&
               (typeof(IEnumerable<>).IsAssignableFrom(propertyType.GetGenericTypeDefinition()) ||
                propertyType.GetGenericTypeDefinition() == typeof(List<>) ||
                propertyType.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                propertyType.GetGenericTypeDefinition() == typeof(IList<>));
    }

    private static JoinInfo GenerateJoinInfo(Type sourceType, Type targetType, ForeignKeyInfo foreignKeyInfo)
    {
        var sourceTableAttr = sourceType.GetCustomAttribute<TableNameAttribute>();
        var targetTableAttr = targetType.GetCustomAttribute<TableNameAttribute>();

        var sourceTableName = sourceTableAttr?.Name ?? sourceType.Name;
        var targetTableName = targetTableAttr?.Name ?? targetType.Name;

        if (foreignKeyInfo.IsCollection)
        {
            var sourcePrimaryKey = GetPrimaryKeyColumn(sourceType);
            return new JoinInfo
            {
                SourceTable = sourceTableName,
                TargetTable = targetTableName,
                SourceForeignKeyColumn = sourcePrimaryKey,
                TargetPrimaryKeyColumn = foreignKeyInfo.ForeignKeyColumnName,
                IsOneToMany = true
            };
        }
        else
        {
            var targetPrimaryKey = GetPrimaryKeyColumn(targetType);
            return new JoinInfo
            {
                SourceTable = sourceTableName,
                TargetTable = targetTableName,
                SourceForeignKeyColumn = foreignKeyInfo.ForeignKeyColumnName,
                TargetPrimaryKeyColumn = targetPrimaryKey,
                IsOneToMany = false
            };
        }
    }

    private static string GetPrimaryKeyColumn(Type type)
    {
        var idProperty = type.GetProperty("Id");
        if (idProperty != null)
        {
            var columnAttr = idProperty.GetCustomAttribute<ColumnNameAttribute>();
            return columnAttr?.Name ?? "Id";
        }

        var firstIntProperty = type.GetProperties()
            .FirstOrDefault(p => p.PropertyType == typeof(int) || p.PropertyType == typeof(int?));

        if (firstIntProperty != null)
        {
            var columnAttr = firstIntProperty.GetCustomAttribute<ColumnNameAttribute>();
            return columnAttr?.Name ?? firstIntProperty.Name;
        }

        throw new InvalidOperationException($"Could not determine primary key for type '{type.Name}'.");
    }
}

public class NavigationPropertyInfo
{
    public string PropertyName { get; set; } = string.Empty;
    public Type SourceType { get; set; } = null!;
    public Type TargetType { get; set; } = null!;
    public ForeignKeyInfo ForeignKeyInfo { get; set; } = null!;
    public JoinInfo JoinInfo { get; set; } = null!;
}

public class ForeignKeyInfo
{
    public string ForeignKeyColumnName { get; set; } = string.Empty;
    public bool IsCollection { get; set; }
}

public class JoinInfo
{
    public string SourceTable { get; set; } = string.Empty;
    public string TargetTable { get; set; } = string.Empty;
    public string SourceForeignKeyColumn { get; set; } = string.Empty;
    public string TargetPrimaryKeyColumn { get; set; } = string.Empty;
    public bool IsOneToMany { get; set; }
}
