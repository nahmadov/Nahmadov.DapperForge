using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace DapperToolkit.Core.Mapping;

internal static class EntityMappingCache<TEntity>
    where TEntity : class
{
    public static readonly EntityMapping Mapping = Build();

    private static EntityMapping Build()
    {
        var type = typeof(TEntity);

        var tableAttr = type.GetCustomAttribute<TableAttribute>();
        var tableName = tableAttr?.Name ?? type.Name;
        string? schema = tableAttr?.Schema;  // ⬅️ Schema dəstəyi

        var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Where(p => 
                            p.CanRead && 
                            p.CanWrite && 
                            p.GetIndexParameters().Length == 0  &&
                            p.GetCustomAttribute<NotMappedAttribute>() is null)
                        .ToArray();

        if (props.Length == 0)
            throw new InvalidOperationException($"Type {type.Name} has no writable public properties.");

        var propertyMappings = props.Select(p =>
        {
            var colAttr = p.GetCustomAttribute<ColumnAttribute>();
            var genAttr = p.GetCustomAttribute<DatabaseGeneratedAttribute>();
            return new PropertyMapping(p, colAttr, genAttr);
        }).ToList();

        var key = props.FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() is not null);

        key ??= props.FirstOrDefault(p => string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase));

        if (key is null)
        {
            var candidate = type.Name + "Id";
            key = props.FirstOrDefault(p => string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase));
        }

        if (key is null)
            throw new InvalidOperationException($"Type {type.Name} has no key property. Define [Key] or an 'Id'/{type.Name}Id property.");

        return new EntityMapping(type, tableName, schema, key, props, propertyMappings);
    }
}