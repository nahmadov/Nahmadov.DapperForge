using System.Linq.Expressions;

using Nahmadov.DapperForge.Core.Mapping;

namespace Nahmadov.DapperForge.Core.Builders;

/// <summary>
/// Builder for configuring a collection navigation (HasMany relationship).
/// </summary>
/// <typeparam name="TEntity">The principal entity type.</typeparam>
/// <typeparam name="TRelated">The dependent (related) entity type.</typeparam>
public class CollectionNavigationBuilder<TEntity, TRelated>
    where TEntity : class
    where TRelated : class
{
    private readonly EntityConfig _entityConfig;
    private readonly RelationshipConfig _relationshipConfig;

    internal CollectionNavigationBuilder(EntityConfig entityConfig, string navigationPropertyName)
    {
        _entityConfig = entityConfig;
        _relationshipConfig = new RelationshipConfig
        {
            NavigationPropertyName = navigationPropertyName,
            PrincipalEntityType = typeof(TEntity),
            DependentEntityType = typeof(TRelated),
            IsReferenceNavigation = false
        };
        _entityConfig.Relationships.Add(_relationshipConfig);
    }

    /// <summary>
    /// Configures this as a many-to-one relationship without specifying the inverse reference.
    /// </summary>
    /// <returns>A builder to configure the foreign key.</returns>
    public CollectionReferenceBuilder<TEntity, TRelated> WithOne()
    {
        return new CollectionReferenceBuilder<TEntity, TRelated>(_relationshipConfig);
    }

    /// <summary>
    /// Configures this as a many-to-one relationship with the inverse reference navigation.
    /// </summary>
    /// <param name="navigationExpression">Expression selecting the reference property on the dependent.</param>
    /// <returns>A builder to configure the foreign key.</returns>
    public CollectionReferenceBuilder<TEntity, TRelated> WithOne(
        Expression<Func<TRelated, TEntity?>> navigationExpression)
    {
        _relationshipConfig.InverseNavigationPropertyName = GetPropertyName(navigationExpression);
        return new CollectionReferenceBuilder<TEntity, TRelated>(_relationshipConfig);
    }

    private static string GetPropertyName<TSource, TProperty>(Expression<Func<TSource, TProperty>> expr)
    {
        if (expr.Body is MemberExpression m)
            return m.Member.Name;
        if (expr.Body is UnaryExpression u && u.Operand is MemberExpression m2)
            return m2.Member.Name;

        throw new InvalidOperationException("Only simple property expressions are supported.");
    }
}
