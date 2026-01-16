using System.Reflection;

using Dapper;

namespace Nahmadov.DapperForge.Core.Mutations.Sql;
/// <summary>
/// Helper class for parameter conversion and merging in mutation operations.
/// </summary>
internal static class MutationParameterHelper
{
    public static Dictionary<string, object?> ConvertToDictionary(object obj)
    {
        var dict = new Dictionary<string, object?>();
        var properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            var value = prop.GetValue(obj);
            dict[prop.Name] = value;
        }

        return dict;
    }

    public static DynamicParameters MergeParameters<TEntity>(TEntity entity, Dictionary<string, object?> whereParams) where TEntity : class
    {
        var parameters = new DynamicParameters(entity);

        foreach (var (key, value) in whereParams)
        {
            parameters.Add(key, value);
        }

        return parameters;
    }
}

