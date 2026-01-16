using System.Reflection;
using Dapper;

namespace Nahmadov.DapperForge.Core.Infrastructure.Extensions;
public class DapperTypeMapExtensions
{
    public static void UseAliasPrefixInsensitiveMapping(params Assembly[] assemblies)
    {
        // Apply to all entity types you want, or call per type.
        // You can also store types in your mapping cache and call SetTypeMap(type).
    }

    public static void SetPrefixInsensitiveMap(Type entityType)
    {
        var map = new CustomPropertyTypeMap(entityType, (type, columnName) =>
        {
            var prop = type.GetProperty(columnName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop is not null) return prop;

            var idx = columnName.LastIndexOf("__", StringComparison.Ordinal);
            if (idx >= 0 && idx + 2 < columnName.Length)
            {
                var trimmed = columnName[(idx + 2)..];
                return type.GetProperty(trimmed,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!;
            }

            return null!;
        });

        SqlMapper.SetTypeMap(entityType, map);
    }
}
