using System.Reflection;

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
    /// Applies all entity type configurations from the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for configuration types.</param>
    /// <param name="predicate">Optional predicate to filter which configuration types to apply.</param>
    public void ApplyConfigurationsFromAssembly(Assembly assembly, Func<Type, bool>? predicate = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var configurationTypes = assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition)
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType &&
                i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)))
            .Where(t => predicate == null || predicate(t));

        foreach (var configurationType in configurationTypes)
        {
            var instance = Activator.CreateInstance(configurationType);
            if (instance == null)
                continue;

            var entityType = configurationType
                .GetInterfaces()
                .First(i => i.IsGenericType &&
                           i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>))
                .GetGenericArguments()[0];

            var applyMethod = typeof(DapperModelBuilder)
                .GetMethod(nameof(ApplyConfiguration))!
                .MakeGenericMethod(entityType);

            applyMethod.Invoke(this, [instance]);
        }
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
        var snapshot = GetSnapshot(config.ClrType);
        return EntityMappingResolver.Resolve(snapshot, config, _defaultSchema);
    }

    private static EntityMetadataSnapshot GetSnapshot(Type clrType)
    {
        var cacheType = typeof(EntityMappingCache<>).MakeGenericType(clrType);
        var snapshotField = cacheType.GetField("Snapshot", BindingFlags.Public | BindingFlags.Static);

        if (snapshotField?.GetValue(null) is not EntityMetadataSnapshot snapshot)
            throw new InvalidOperationException($"Failed to retrieve metadata snapshot for {clrType.Name}.");

        return snapshot;
    }
}
