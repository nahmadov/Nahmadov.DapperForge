using System.Linq.Expressions;

using Nahmadov.DapperForge.Core.Mapping;

namespace Nahmadov.DapperForge.Core.Builders;

/// <summary>
/// Builder for configuring the FK side of a one-to-many relationship from the collection side.
/// </summary>
/// <typeparam name="TEntity">The principal entity type.</typeparam>
/// <typeparam name="TRelated">The dependent entity type (where the FK resides).</typeparam>
public class CollectionReferenceBuilder<TEntity, TRelated>
    where TEntity : class
    where TRelated : class
{
    private readonly RelationshipConfig _relationshipConfig;

    internal CollectionReferenceBuilder(RelationshipConfig relationshipConfig)
    {
        _relationshipConfig = relationshipConfig;
    }

    /// <summary>
    /// Specifies the foreign key property on the dependent entity.
    /// </summary>
    /// <param name="foreignKeyExpression">Expression selecting the FK property on the dependent.</param>
    /// <returns>This builder for method chaining.</returns>
    public CollectionReferenceBuilder<TEntity, TRelated> HasForeignKey(
        Expression<Func<TRelated, object?>> foreignKeyExpression)
    {
        _relationshipConfig.ForeignKeyPropertyName = GetPropertyName(foreignKeyExpression);
        return this;
    }

    /// <summary>
    /// Specifies the principal key property on the principal entity.
    /// Defaults to the primary key if not specified.
    /// </summary>
    /// <param name="principalKeyExpression">Expression selecting the principal key.</param>
    /// <returns>This builder for method chaining.</returns>
    public CollectionReferenceBuilder<TEntity, TRelated> HasPrincipalKey(
        Expression<Func<TEntity, object?>> principalKeyExpression)
    {
        _relationshipConfig.PrincipalKeyPropertyName = GetPropertyName(principalKeyExpression);
        return this;
    }

    private static string GetPropertyName<TSource>(Expression<Func<TSource, object?>> expr)
    {
        if (expr.Body is MemberExpression m)
            return m.Member.Name;
        if (expr.Body is UnaryExpression u && u.Operand is MemberExpression m2)
            return m2.Member.Name;

        throw new InvalidOperationException("Only simple property expressions are supported.");
    }
}
