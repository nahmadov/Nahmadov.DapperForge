using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using DapperToolkit.Core.Attributes;

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
                            p.GetIndexParameters().Length == 0 &&
                            p.GetCustomAttribute<NotMappedAttribute>() is null)
                        .ToArray();

        if (props.Length == 0)
            throw new InvalidOperationException($"Type {type.Name} has no writable public properties.");

        var propertyMappings = props.Select(p =>
        {
            var colAttr = p.GetCustomAttribute<ColumnAttribute>();
            var genAttr = p.GetCustomAttribute<DatabaseGeneratedAttribute>();
            var required = p.GetCustomAttribute<KeyAttribute>() is not null
                           || p.GetCustomAttribute<RequiredAttribute>() is not null;

            var stringLength = p.GetCustomAttribute<StringLengthAttribute>();
            var maxLengthAttr = p.GetCustomAttribute<MaxLengthAttribute>();
            var maxLength = stringLength?.MaximumLength > 0
                ? stringLength.MaximumLength
                : maxLengthAttr?.Length > 0 ? maxLengthAttr.Length : (int?)null;

            return new PropertyMapping(p, colAttr, genAttr, false, required, maxLength);
        }).ToList();

        var isReadOnly = type.GetCustomAttribute<ReadOnlyEntityAttribute>() is not null;

        var keyProps = props
            .Where(p => p.GetCustomAttribute<KeyAttribute>() is not null)
            .ToList();

        if (keyProps.Count == 0)
        {
            var single = props.FirstOrDefault(p => string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase))
                      ?? props.FirstOrDefault(p => string.Equals(p.Name, type.Name + "Id", StringComparison.OrdinalIgnoreCase))
                      ?? props.FirstOrDefault(p => NormalizeKeyName(p.Name) == NormalizeKeyName(type.Name + "Id"));

            if (single is not null)
                keyProps.Add(single);
        }

        if (keyProps.Count == 0 && !isReadOnly)
            throw new InvalidOperationException(
                $"Type {type.Name} has no key property. Define [Key] or an 'Id'/{type.Name}Id property.");

        return new EntityMapping(type, tableName, schema, keyProps, props, propertyMappings, isReadOnly);
    }

    private static string NormalizeKeyName(string name)
        => new(name.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
