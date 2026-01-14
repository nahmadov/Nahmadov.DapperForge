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

    public ISqlDialect Dialect => _dialect;

    public string? DefaultSchema => _defaultSchema;

    /// <summary>
    /// Starts configuration for an entity type.
    /// </summary>
    public EntityTypeBuilder<TEntity> Entity<TEntity>()
        where TEntity : class
    {
        var config = GetOrCreateConfig(typeof(TEntity));
        return new EntityTypeBuilder<TEntity>(config);
    }

    /// <summary>
    /// Configures an entity type using the provided callback.
    /// </summary>
    public EntityTypeBuilder<TEntity> Entity<TEntity>(Action<EntityTypeBuilder<TEntity>> configure)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = Entity<TEntity>();
        configure(builder);
        return builder;
    }

    /// <summary>
    /// Starts configuration for an entity type using a runtime type.
    /// </summary>
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
    public void ApplyConfiguration<TEntity>(IEntityTypeConfiguration<TEntity> configuration)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var builder = Entity<TEntity>();
        configuration.Configure(builder);
    }

    /// <summary>
    /// Applies all entity type configurations from the specified assembly.
    /// </summary>
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
        _entities[clrType] = config;
        return config;
    }

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
