using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

using DapperToolkit.Core.Attributes;
using DapperToolkit.Core.Interfaces;
using DapperToolkit.Core.Mapping;

namespace DapperToolkit.Core.Builders;

public class DapperModelBuilder(ISqlDialect dialect, string? defaultSchema = null)
{
    private readonly ISqlDialect _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
    private readonly string? _defaultSchema = defaultSchema;
    private readonly Dictionary<Type, EntityConfig> _entities = [];

    public ISqlDialect Dialect => _dialect;
    public string? DefaultSchema => _defaultSchema;

    public EntityTypeBuilder<TEntity> Entity<TEntity>()
    {
        var config = GetOrCreateConfig(typeof(TEntity));
        return new EntityTypeBuilder<TEntity>(config);
    }

    public EntityTypeBuilder<TEntity> Entity<TEntity>(Action<EntityTypeBuilder<TEntity>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = Entity<TEntity>();
        configure(builder);
        return builder;
    }

    public IEntityTypeBuilder Entity(Type clrType)
    {
        ArgumentNullException.ThrowIfNull(clrType);

        var config = GetOrCreateConfig(clrType);
        var builderType = typeof(EntityTypeBuilder<>).MakeGenericType(clrType);
        return (IEntityTypeBuilder)Activator.CreateInstance(builderType, config)!;
    }

    public IEntityTypeBuilder Entity(Type clrType, Action<IEntityTypeBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = Entity(clrType);
        configure(builder);
        return builder;
    }

    public void ApplyConfiguration<TEntity>(IEntityTypeConfiguration<TEntity> configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var builder = Entity<TEntity>();
        configuration.Configure(builder);
    }

    public IReadOnlyDictionary<Type, EntityMapping> Build()
    {
        var mappings = new Dictionary<Type, EntityMapping>();

        foreach (var config in _entities.Values)
        {
            var mapping = BuildEntityMapping(config);
            mappings[config.ClrType] = mapping;
        }

        return mappings;
    }

    private EntityConfig GetOrCreateConfig(Type clrType)
    {
        if (_entities.TryGetValue(clrType, out var existing))
            return existing;

        var config = new EntityConfig(clrType);
        var tableAttr = clrType.GetCustomAttribute<TableAttribute>();
        if (tableAttr is not null && !string.IsNullOrWhiteSpace(tableAttr.Name))
        {
            config.SetTable(tableAttr.Name, tableAttr.Schema);
        }

        if (clrType.GetCustomAttribute<ReadOnlyEntityAttribute>() is not null)
        {
            config.SetReadOnly(true);
        }

        _entities[clrType] = config;
        return config;
    }

    private EntityMapping BuildEntityMapping(EntityConfig config)
    {
        var type = config.ClrType;
        var tableAttr = type.GetCustomAttribute<TableAttribute>();
        var readOnlyAttr = type.GetCustomAttribute<ReadOnlyEntityAttribute>();

        var tableName = string.IsNullOrWhiteSpace(config.TableName)
            ? tableAttr?.Name ?? type.Name
            : config.TableName;

        var schema = config.Schema ?? tableAttr?.Schema ?? _defaultSchema;
        var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p =>
                p.CanRead &&
                p.CanWrite &&
                p.GetIndexParameters().Length == 0 &&
                p.GetCustomAttribute<NotMappedAttribute>() is null)
            .ToArray();

        if (props.Length == 0)
        {
            throw new InvalidOperationException($"Type {type.Name} has no writable public properties.");
        }

        var propertyMappings = new List<PropertyMapping>();

        foreach (var prop in props)
        {
            config.Properties.TryGetValue(prop.Name, out var propConfig);

            var columnAttr = prop.GetCustomAttribute<ColumnAttribute>();
            var databaseGeneratedAttr = prop.GetCustomAttribute<DatabaseGeneratedAttribute>();

            ColumnAttribute? effectiveColumnAttr = columnAttr;
            if (propConfig is not null && !string.IsNullOrWhiteSpace(propConfig.ColumnName))
            {
                effectiveColumnAttr = new ColumnAttribute(propConfig.ColumnName);
            }

            var required =
                (propConfig?.IsRequired ?? false) ||
                prop.GetCustomAttribute<RequiredAttribute>() is not null;

            int? maxLength = propConfig?.MaxLength;
            var stringLengthAttr = prop.GetCustomAttribute<StringLengthAttribute>();
            if (maxLength is null && stringLengthAttr?.MaximumLength > 0)
                maxLength = stringLengthAttr.MaximumLength;

            var maxLengthAttr = prop.GetCustomAttribute<MaxLengthAttribute>();
            if (maxLength is null && maxLengthAttr?.Length > 0)
                maxLength = maxLengthAttr.Length;

            var mapping = new PropertyMapping(
                prop,
                effectiveColumnAttr,
                databaseGeneratedAttr,
                propConfig?.IsReadOnly ?? false,
                required,
                maxLength,
                propConfig?.SequenceName);

            propertyMappings.Add(mapping);
        }

        var keyProps = new List<PropertyInfo>();
        if (config.HasKey)
        {
            if (config.KeyProperties.Count > 0)
            {
                foreach (var keyName in config.KeyProperties)
                {
                    var keyProp = props.FirstOrDefault(p => string.Equals(p.Name, keyName, StringComparison.Ordinal))
                        ?? throw new InvalidOperationException(
                            $"Key property '{keyName}' not found on entity type '{type.Name}'.");
                    keyProps.Add(keyProp);
                }
            }
            else
            {
                var attrs = props.Where(p => p.GetCustomAttribute<KeyAttribute>() is not null).ToList();
                if (attrs.Count > 0)
                {
                    keyProps.AddRange(attrs);
                }
                else
                {
                    var convention = props.FirstOrDefault(p => string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase))
                        ?? props.FirstOrDefault(p => string.Equals(p.Name, type.Name + "Id", StringComparison.OrdinalIgnoreCase));
                    if (convention is not null)
                        keyProps.Add(convention);
                }
            }

            if (keyProps.Count == 0 && !config.IsReadOnly && readOnlyAttr is null)
            {
                throw new InvalidOperationException(
                    $"Type {type.Name} has no key property. Define HasKey(...) or mark the entity as read-only/HasNoKey.");
            }
        }

        var isReadOnly = config.IsReadOnly || readOnlyAttr is not null;

        return new EntityMapping(
            type,
            tableName,
            schema,
            keyProps,
            props,
            propertyMappings,
            isReadOnly);
    }
}
