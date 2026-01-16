using System.Linq.Expressions;
using System.Reflection;

namespace Nahmadov.DapperForge.Core.Querying.Predicates;
/// <summary>
/// Helper methods for entity property inspection.
/// </summary>
internal static class EntityPropertyHelper
{
    public static bool IsEntityProperty<TEntity>(MemberExpression node) where TEntity : class
    {
        return typeof(TEntity).IsAssignableFrom(node.Expression?.Type ?? typeof(object)) &&
               node.Member is PropertyInfo;
    }

    public static bool IsStringProperty<TEntity>(MemberExpression node) where TEntity : class
    {
        return IsEntityProperty<TEntity>(node) &&
               ((PropertyInfo)node.Member).PropertyType == typeof(string);
    }
}

