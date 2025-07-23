using System.Reflection;
using DapperToolkit.Core.Attributes;

namespace DapperToolkit.Core.Common;

public class MultiLevelResultMapper<T> where T : class
{
    private readonly List<IncludeLevel> _includeLevels;
    private readonly Dictionary<string, Dictionary<object, object>> _entityCache = new();

    public MultiLevelResultMapper(List<IncludeLevel> includeLevels)
    {
        _includeLevels = includeLevels;
        
        foreach (var level in _includeLevels)
        {
            _entityCache[level.Alias] = new Dictionary<object, object>();
        }
    }

    public IEnumerable<T> MapResults(IEnumerable<dynamic> rawResults)
    {
        var rootEntities = new Dictionary<object, T>();

        foreach (var row in rawResults)
        {
            var rowDict = (IDictionary<string, object>)row;
            var entities = new Dictionary<string, object>();

            foreach (var level in _includeLevels)
            {
                var entityData = ExtractEntityData(rowDict, level.Alias);
                var entityKey = GetEntityKey(entityData, level.Type);

                if (!_entityCache[level.Alias].ContainsKey(entityKey))
                {
                    var entity = MapDynamicToEntity(entityData, level.Type);
                    _entityCache[level.Alias][entityKey] = entity;
                    entities[level.Alias] = entity;
                }
                else
                {
                    entities[level.Alias] = _entityCache[level.Alias][entityKey];
                }
            }

            BuildRelationships(entities);

            var rootEntity = (T)entities["r0"];
            var rootKey = GetEntityKey(ExtractEntityData(rowDict, "r0"), typeof(T));
            
            if (!rootEntities.ContainsKey(rootKey))
            {
                rootEntities[rootKey] = rootEntity;
            }
        }

        return rootEntities.Values;
    }

    private Dictionary<string, object> ExtractEntityData(IDictionary<string, object> row, string alias)
    {
        var entityData = new Dictionary<string, object>();
        var prefix = $"{alias}_";

        foreach (var kvp in row)
        {
            if (kvp.Key.StartsWith(prefix))
            {
                var columnName = kvp.Key.Substring(prefix.Length);
                entityData[columnName] = kvp.Value;
            }
        }

        return entityData;
    }

    private object GetEntityKey(Dictionary<string, object> entityData, Type entityType)
    {
        var primaryKeyColumnName = PrimaryKeyHelper.GetPrimaryKeyColumnName(entityType);
        if (primaryKeyColumnName != null)
        {
            return entityData.ContainsKey(primaryKeyColumnName) ? entityData[primaryKeyColumnName] : 0;
        }
        return 0;
    }

    private object MapDynamicToEntity(Dictionary<string, object> data, Type entityType)
    {
        var entity = Activator.CreateInstance(entityType) ?? throw new InvalidOperationException($"Failed to create instance of {entityType.Name}");
        
        foreach (var property in entityType.GetProperties())
        {
            if (IsNavigationProperty(property))
                continue;

            var columnAttr = property.GetCustomAttribute<ColumnNameAttribute>();
            var columnName = columnAttr?.Name ?? property.Name;
            
            if (data.ContainsKey(columnName) && data[columnName] != DBNull.Value)
            {
                try
                {
                    var value = Convert.ChangeType(data[columnName], property.PropertyType);
                    property.SetValue(entity, value);
                }
                catch
                {
                }
            }
        }
        
        return entity;
    }

    private void BuildRelationships(Dictionary<string, object> entities)
    {
        for (int i = 1; i < _includeLevels.Count; i++)
        {
            var level = _includeLevels[i];
            var parentLevel = _includeLevels[i - 1];

            var parentEntity = entities[parentLevel.Alias];
            var childEntity = entities[level.Alias];

            var navigationProperty = GetNavigationProperty(parentLevel.Type, level.NavigationInfo!);
            if (navigationProperty == null) continue;

            if (level.NavigationInfo!.ForeignKeyInfo.IsCollection)
            {
                var collection = navigationProperty.GetValue(parentEntity) as System.Collections.IList;
                if (collection != null && !CollectionContains(collection, childEntity))
                {
                    collection.Add(childEntity);
                }
            }
            else
            {
                navigationProperty.SetValue(parentEntity, childEntity);
            }

            SetReverseNavigation(childEntity, parentEntity, level.NavigationInfo);
        }
    }

    private PropertyInfo? GetNavigationProperty(Type entityType, NavigationPropertyInfo navigationInfo)
    {
        return entityType.GetProperty(navigationInfo.PropertyName);
    }

    private void SetReverseNavigation(object childEntity, object parentEntity, NavigationPropertyInfo navigationInfo)
    {
        var reverseProperty = childEntity.GetType().GetProperties()
            .FirstOrDefault(p => p.PropertyType == parentEntity.GetType());

        if (reverseProperty != null)
        {
            reverseProperty.SetValue(childEntity, parentEntity);
        }
    }

    private bool CollectionContains(System.Collections.IList collection, object item)
    {
        var itemId = GetEntityKey(new Dictionary<string, object>(), item.GetType());
        
        foreach (var existingItem in collection)
        {
            var existingId = GetEntityKey(new Dictionary<string, object>(), existingItem.GetType());
            if (Equals(itemId, existingId))
                return true;
        }
        return false;
    }

    private bool IsNavigationProperty(PropertyInfo property)
    {
        return property.GetCustomAttribute<ForeignKeyAttribute>() != null ||
               property.GetCustomAttribute<InversePropertyAttribute>() != null ||
               (!property.PropertyType.IsPrimitive && 
                property.PropertyType != typeof(string) && 
                property.PropertyType != typeof(DateTime) && 
                property.PropertyType != typeof(decimal) && 
                property.PropertyType != typeof(Guid) &&
                !property.PropertyType.IsValueType);
    }
}
