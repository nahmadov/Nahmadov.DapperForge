using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

using Nahmadov.DapperForge.Core.Attributes;
using Nahmadov.DapperForge.Core.Interfaces;
using Nahmadov.DapperForge.Core.Mapping;

namespace Nahmadov.DapperForge.Core.Builders;

/// <summary>
/// Provides fluent configuration for mapping CLR types to database tables for Dapper.
/// </summary>
/// <param name="dialect">SQL dialect used when generating identifiers and SQL fragments.</param>
/// <param name="defaultSchema">Optional default schema to apply when no schema is specified.</param>
public class DapperModelBuilder(ISqlDialect dialect, string? defaultSchema = null)
{
    private readonly ISqlDialect _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
    private readonly string? _defaultSchema = defaultSchema;
    private readonly Dictionary<Type, EntityConfig> _entities = [];

    /// <summary>
    /// Gets the SQL dialect used by the model builder.
    /// </summary>
    public ISqlDialect Dialect => _dialect;

    /// <summary>
    /// Gets the default schema applied when an entity does not specify a schema.
    /// </summary>
    public string? DefaultSchema => _defaultSchema;

    /// <summary>
    /// Starts configuration for an entity type using a generic type parameter.
    /// </summary>
    /// <typeparam name="TEntity">The CLR entity type to configure.</typeparam>
    /// <returns>An <see cref="EntityTypeBuilder{TEntity}"/> for the entity type.</returns>
    public EntityTypeBuilder<TEntity> Entity<TEntity>()
    {
        var config = GetOrCreateConfig(typeof(TEntity));
        return new EntityTypeBuilder<TEntity>(config);
    }

    /// <summary>
    /// Configures an entity type using the provided callback.
    /// </summary>
    /// <typeparam name="TEntity">The CLR entity type to configure.</typeparam>
    /// <param name="configure">Delegate that applies configuration to the entity builder.</param>
    /// <returns>An <see cref="EntityTypeBuilder{TEntity}"/> for the entity type.</returns>
    public EntityTypeBuilder<TEntity> Entity<TEntity>(Action<EntityTypeBuilder<TEntity>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = Entity<TEntity>();
        configure(builder);
        return builder;
    }

    /// <summary>
    /// Starts configuration for an entity type using a runtime <see cref="Type"/>.
    /// </summary>
    /// <param name="clrType">The CLR entity type to configure.</param>
    /// <returns>An <see cref="IEntityTypeBuilder"/> for the entity type.</returns>
    public IEntityTypeBuilder Entity(Type clrType)
    {
        ArgumentNullException.ThrowIfNull(clrType);

        var config = GetOrCreateConfig(clrType);
        var builderType = typeof(EntityTypeBuilder<>).MakeGenericType(clrType);
        return (IEntityTypeBuilder)Activator.CreateInstance(builderType, config)!;
    }

    /// <summary>
    /// Configures an entity type using the provided callback for runtime types.
    /// </summary>
    /// <param name="clrType">The CLR entity type to configure.</param>
    /// <param name="configure">Delegate that applies configuration to the entity builder.</param>
    /// <returns>An <see cref="IEntityTypeBuilder"/> for the entity type.</returns>
    public IEntityTypeBuilder Entity(Type clrType, Action<IEntityTypeBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = Entity(clrType);
        configure(builder);
        return builder;
    }

    /// <summary>
    /// Applies a reusable configuration instance to the corresponding entity type.
    /// </summary>
    /// <typeparam name="TEntity">The CLR entity type being configured.</typeparam>
    /// <param name="configuration">Configuration class implementing entity settings.</param>
    public void ApplyConfiguration<TEntity>(IEntityTypeConfiguration<TEntity> configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var builder = Entity<TEntity>();
        configuration.Configure(builder);
    }

    /// <summary>
    /// Builds immutable entity mappings based on the collected configurations and attributes.
    /// </summary>
    /// <returns>A read-only dictionary of entity mappings keyed by CLR type.</returns>
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

    /// <summary>
    /// Retrieves an existing entity configuration or creates a new one for the given CLR type.
    /// </summary>
    /// <param name="clrType">The CLR entity type being configured.</param>
    /// <returns>The <see cref="EntityConfig"/> associated with the CLR type.</returns>
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

    /// <summary>
    /// Builds an <see cref="EntityMapping"/> instance from the provided configuration and reflection metadata.
    /// </summary>
    /// <param name="config">The configuration data collected for the entity type.</param>
    /// <returns>An immutable mapping describing table, schema, keys, and properties.</returns>
    private EntityMapping BuildEntityMapping(EntityConfig config)
    {
        var type = config.ClrType;
        var tableAttr = type.GetCustomAttribute<TableAttribute>();
        var readOnlyAttr = type.GetCustomAttribute<ReadOnlyEntityAttribute>();

        var tableName = !string.IsNullOrWhiteSpace(config.TableName)
            ? config.TableName
            : tableAttr?.Name ?? type.Name;

        var schema = !string.IsNullOrWhiteSpace(config.Schema)
            ? config.Schema
            : tableAttr?.Schema ?? _defaultSchema;
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

            var isKey = keyProps.Contains(prop);
            var autoGenerated = propConfig?.IsAutoGenerated ?? (isKey && config.HasKey && keyProps.Count == 1);
            if (databaseGeneratedAttr is null && autoGenerated && string.IsNullOrWhiteSpace(propConfig?.SequenceName))
            {
                databaseGeneratedAttr = new DatabaseGeneratedAttribute(DatabaseGeneratedOption.Identity);
            }

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
