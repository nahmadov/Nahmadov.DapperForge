using System.Linq.Expressions;

using DapperToolkit.Core.Mapping;

namespace DapperToolkit.Core.Builders;

public class EntityTypeBuilder<TEntity>(EntityConfig entity) : IEntityTypeBuilder
{
    private readonly EntityConfig _entity = entity;

    public string? TableName => _entity.TableName;
    public string? Schema => _entity.Schema;

    public EntityTypeBuilder<TEntity> ToTable(string tableName, string? schema = null)
    {
        _entity.SetTable(tableName, schema);
        return this;
    }

    IEntityTypeBuilder IEntityTypeBuilder.ToTable(string tableName, string? schema)
        => ToTable(tableName, schema);

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

    public EntityTypeBuilder<TEntity> HasNoKey()
    {
        _entity.KeyProperties.Clear();
        _entity.SetNoKey();
        return this;
    }

    IEntityTypeBuilder IEntityTypeBuilder.HasNoKey() => HasNoKey();

    public EntityTypeBuilder<TEntity> IsReadOnly()
    {
        _entity.SetReadOnly(true);
        return this;
    }

    IEntityTypeBuilder IEntityTypeBuilder.IsReadOnly() => IsReadOnly();

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

    private static string GetPropertyName(Expression<Func<TEntity, object?>> expr)
    {
        if (expr.Body is MemberExpression m)
            return m.Member.Name;
        if (expr.Body is UnaryExpression u && u.Operand is MemberExpression m2)
            return m2.Member.Name;

        throw new InvalidOperationException("Only simple property expressions are supported.");
    }
}
