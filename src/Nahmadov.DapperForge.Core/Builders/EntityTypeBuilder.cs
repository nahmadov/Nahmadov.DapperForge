using System.Linq.Expressions;

using Nahmadov.DapperForge.Core.Mapping;

namespace Nahmadov.DapperForge.Core.Builders;

/// <summary>
/// Fluent builder for configuring entity mappings.
/// </summary>
public class EntityTypeBuilder<TEntity>(EntityConfig entity) : IEntityTypeBuilder
    where TEntity : class
{
    private readonly EntityConfig _entity = entity;

    /// <summary>
    /// Gets the configured table name, if set.
    /// </summary>
    public string? TableName => _entity.TableName;

    /// <summary>
    /// Gets the configured schema name, if set.
    /// </summary>
    public string? Schema => _entity.Schema;

    /// <summary>
    /// Configures the table name and optional schema for the entity.
    /// </summary>
    /// <param name="tableName">Target table name.</param>
    /// <param name="schema">Optional schema.</param>
    /// <returns>The current builder for chaining.</returns>
    public EntityTypeBuilder<TEntity> ToTable(string tableName, string? schema = null)
    {
        _entity.SetTable(tableName, schema);
        return this;
    }

    IEntityTypeBuilder IEntityTypeBuilder.ToTable(string tableName, string? schema)
        => ToTable(tableName, schema);

    /// <summary>
    /// Configures the primary key using property selector expressions.
    /// </summary>
    /// <param name="keyExpressions">Expressions pointing to key properties.</param>
    /// <returns>The current builder for chaining.</returns>
    public EntityTypeBuilder<TEntity> HasKey(params Expression<Func<TEntity, object?>>[] keyExpressions)
    {
        if (keyExpressions.Length == 0)
            throw new ArgumentException("At least one key expression is required.", nameof(keyExpressions));

        _entity.KeyProperties.Clear();
        foreach (var expr in keyExpressions)
        {
            var name = GetPropertyName(expr);
            _entity.KeyProperties.Add(name);
        }
        return this;
    }

    /// <summary>
    /// Configures an alternate key (business key) using property selector expressions.
    /// Alternate keys represent business-level uniqueness and are used for Update/Delete
    /// operations when a primary key doesn't exist.
    /// </summary>
    /// <param name="keyExpressions">Expressions pointing to alternate key properties.</param>
    /// <returns>The current builder for chaining.</returns>
    /// <remarks>
    /// Alternate keys should be backed by a unique constraint or unique index in the database.
    /// Examples: employee number, email, account number, etc.
    /// </remarks>
    public EntityTypeBuilder<TEntity> HasAlternateKey(params Expression<Func<TEntity, object?>>[] keyExpressions)
    {
        if (keyExpressions.Length == 0)
            throw new ArgumentException("At least one alternate key expression is required.", nameof(keyExpressions));

        _entity.AlternateKeyProperties.Clear();
        foreach (var expr in keyExpressions)
        {
            var name = GetPropertyName(expr);
            _entity.AlternateKeyProperties.Add(name);
        }
        return this;
    }

    /// <summary>
    /// Marks the entity as keyless.
    /// </summary>
    /// <returns>The current builder for chaining.</returns>
    public EntityTypeBuilder<TEntity> HasNoKey()
    {
        _entity.KeyProperties.Clear();
        _entity.SetNoKey();
        return this;
    }

    IEntityTypeBuilder IEntityTypeBuilder.HasNoKey() => HasNoKey();

    /// <summary>
    /// Marks the entity as read-only.
    /// </summary>
    /// <returns>The current builder for chaining.</returns>
    public EntityTypeBuilder<TEntity> IsReadOnly()
    {
        _entity.SetReadOnly(true);
        return this;
    }

    IEntityTypeBuilder IEntityTypeBuilder.IsReadOnly() => IsReadOnly();

    /// <summary>
    /// Configures an individual property using a selector expression.
    /// </summary>
    /// <param name="propertyExpression">Expression pointing to the property.</param>
    /// <returns>A <see cref="PropertyBuilder"/> for further configuration.</returns>
    public PropertyBuilder Property(Expression<Func<TEntity, object?>> propertyExpression)
    {
        var name = GetPropertyName(propertyExpression);
        if (!_entity.Properties.TryGetValue(name, out var propConfig))
        {
            propConfig = new PropertyConfig(name);
            _entity.Properties[name] = propConfig;
        }
        return new PropertyBuilder(propConfig);
    }

    /// <summary>
    /// Configures a reference navigation to a related entity (one-to-one or many-to-one from dependent side).
    /// </summary>
    /// <typeparam name="TRelated">The principal entity type.</typeparam>
    /// <param name="navigationExpression">Expression selecting the navigation property.</param>
    /// <returns>A builder to further configure the relationship.</returns>
    public ReferenceNavigationBuilder<TEntity, TRelated> HasOne<TRelated>(
        Expression<Func<TEntity, TRelated?>> navigationExpression)
        where TRelated : class
    {
        var navigationName = GetPropertyName(navigationExpression);
        return new ReferenceNavigationBuilder<TEntity, TRelated>(_entity, navigationName);
    }

    /// <summary>
    /// Configures a collection navigation to related entities (one-to-many from principal side).
    /// </summary>
    /// <typeparam name="TRelated">The dependent entity type.</typeparam>
    /// <param name="navigationExpression">Expression selecting the collection navigation property.</param>
    /// <returns>A builder to further configure the relationship.</returns>
    public CollectionNavigationBuilder<TEntity, TRelated> HasMany<TRelated>(
        Expression<Func<TEntity, IEnumerable<TRelated>?>> navigationExpression)
        where TRelated : class
    {
        var navigationName = GetPropertyName(navigationExpression);
        return new CollectionNavigationBuilder<TEntity, TRelated>(_entity, navigationName);
    }

    /// <summary>
    /// Extracts the property name from a simple member access expression.
    /// </summary>
    /// <param name="expr">Expression pointing to a property.</param>
    /// <returns>Property name.</returns>
    private static string GetPropertyName(Expression<Func<TEntity, object?>> expr)
    {
        if (expr.Body is MemberExpression m)
            return m.Member.Name;
        if (expr.Body is UnaryExpression u && u.Operand is MemberExpression m2)
            return m2.Member.Name;

        throw new InvalidOperationException("Only simple property expressions are supported.");
    }

    private static string GetPropertyName<TProperty>(Expression<Func<TEntity, TProperty>> expr)
    {
        if (expr.Body is MemberExpression m)
            return m.Member.Name;
        if (expr.Body is UnaryExpression u && u.Operand is MemberExpression m2)
            return m2.Member.Name;

        throw new InvalidOperationException("Only simple property expressions are supported.");
    }
}
