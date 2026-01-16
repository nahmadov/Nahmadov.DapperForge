using System.Linq.Expressions;

using Nahmadov.DapperForge.Core.Modeling.Mapping;

namespace Nahmadov.DapperForge.Core.Modeling.Builders;
/// <summary>
/// Builder for configuring a reference navigation (HasOne relationship).
/// </summary>
/// <typeparam name="TEntity">The dependent entity type.</typeparam>
/// <typeparam name="TRelated">The principal (related) entity type.</typeparam>
public class ReferenceNavigationBuilder<TEntity, TRelated>
    where TEntity : class
    where TRelated : class
{
    private readonly EntityConfig _entityConfig;
    private readonly RelationshipConfig _relationshipConfig;

    internal ReferenceNavigationBuilder(EntityConfig entityConfig, string navigationPropertyName)
    {
        _entityConfig = entityConfig;
        _relationshipConfig = new RelationshipConfig
        {
            NavigationPropertyName = navigationPropertyName,
            PrincipalEntityType = typeof(TRelated),
            DependentEntityType = typeof(TEntity),
            IsReferenceNavigation = true
        };
        _entityConfig.Relationships.Add(_relationshipConfig);
    }

    /// <summary>
    /// Configures this as a one-to-many relationship without specifying the inverse collection.
    /// </summary>
    /// <returns>A builder to configure the foreign key.</returns>
    public ReferenceCollectionBuilder<TEntity, TRelated> WithMany()
    {
        return new ReferenceCollectionBuilder<TEntity, TRelated>(_relationshipConfig);
    }

    /// <summary>
    /// Configures this as a one-to-many relationship with the inverse collection navigation.
    /// </summary>
    /// <param name="navigationExpression">Expression selecting the collection property on the principal.</param>
    /// <returns>A builder to configure the foreign key.</returns>
    public ReferenceCollectionBuilder<TEntity, TRelated> WithMany(
        Expression<Func<TRelated, IEnumerable<TEntity>?>> navigationExpression)
    {
        _relationshipConfig.InverseNavigationPropertyName = GetPropertyName(navigationExpression);
        return new ReferenceCollectionBuilder<TEntity, TRelated>(_relationshipConfig);
    }

    /// <summary>
    /// Configures this as a one-to-one relationship without specifying the inverse reference.
    /// </summary>
    /// <returns>A builder to configure the foreign key.</returns>
    public ReferenceReferenceBuilder<TEntity, TRelated> WithOne()
    {
        return new ReferenceReferenceBuilder<TEntity, TRelated>(_relationshipConfig);
    }

    /// <summary>
    /// Configures this as a one-to-one relationship with the inverse reference navigation.
    /// </summary>
    /// <param name="navigationExpression">Expression selecting the reference property on the principal.</param>
    /// <returns>A builder to configure the foreign key.</returns>
    public ReferenceReferenceBuilder<TEntity, TRelated> WithOne(
        Expression<Func<TRelated, TEntity?>> navigationExpression)
    {
        _relationshipConfig.InverseNavigationPropertyName = GetPropertyName(navigationExpression);
        return new ReferenceReferenceBuilder<TEntity, TRelated>(_relationshipConfig);
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


