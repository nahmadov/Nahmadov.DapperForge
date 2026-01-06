using System.Reflection;

using Nahmadov.DapperForge.Core.Mapping;

namespace Nahmadov.DapperForge.Core.Context.Utilities;

/// <summary>
/// Builds parameter dictionaries for entity keys from various input representations.
/// </summary>
internal static class KeyParameterBuilder
{
    public static Dictionary<string, object?> Build(EntityMapping mapping, object key, string entityName)
    {
        if (mapping.KeyProperties.Count == 1)
        {
            return new Dictionary<string, object?>
            {
                [mapping.KeyProperties[0].Name] = key
            };
        }

        if (key is IDictionary<string, object?> dict)
        {
            var result = new Dictionary<string, object?>();
            foreach (var kp in mapping.KeyProperties)
            {
                if (!dict.TryGetValue(kp.Name, out var value))
                {
                    throw new InvalidOperationException(
                        $"Key parameter missing value for '{kp.Name}' for entity '{entityName}'.");
                }
                result[kp.Name] = value;
            }
            return result;
        }

        var keyType = key.GetType();
        var resultFromObject = new Dictionary<string, object?>();
        foreach (var kp in mapping.KeyProperties)
        {
            var prop = keyType.GetProperty(kp.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (prop is null)
            {
                throw new InvalidOperationException(
                    $"Key object does not contain property '{kp.Name}' required for entity '{entityName}'.");
            }

            resultFromObject[kp.Name] = prop.GetValue(key);
        }

        return resultFromObject;
    }
}
