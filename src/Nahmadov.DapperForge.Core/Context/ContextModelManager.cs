using System.Reflection;

using Nahmadov.DapperForge.Core.Modeling.Builders;
using Nahmadov.DapperForge.Core.Context.Options;
using Nahmadov.DapperForge.Core.Infrastructure.Exceptions;
using Nahmadov.DapperForge.Core.Infrastructure.Extensions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;

namespace Nahmadov.DapperForge.Core.Context;
/// <summary>
/// Handles model building and mapping retrieval for a <see cref="DapperDbContext"/>.
/// </summary>
internal sealed class ContextModelManager(DapperDbContextOptions options, Type contextType, Action<DapperModelBuilder> configureModel)
{
    private readonly DapperDbContextOptions _options = options;
    private readonly Type _contextType = contextType;
    private readonly Action<DapperModelBuilder> _configureModel = configureModel;
    private readonly Dictionary<Type, EntityMapping> _model = [];
    private readonly HashSet<Type> _registeredEntityTypes = [];
    private readonly object _modelBuildLock = new();
    private volatile bool _modelBuilt;

    public void EnsureModelBuilt()
    {
        // Double-check locking pattern for thread-safety with minimal performance impact
        if (_modelBuilt) return;

        lock (_modelBuildLock)
        {
            if (_modelBuilt) return;

            var builder = new DapperModelBuilder(_options.Dialect!, _options.Dialect?.DefaultSchema);

            InitializeMappingsFromAttributes(builder);
            _configureModel(builder);
            ApplyDbSetNameConvention(builder);
            RegisterDbSetEntityTypes();

            foreach (var kvp in builder.Build())
            {
                _model[kvp.Key] = kvp.Value;
            }

            foreach (var kv in _model)
            {
                DapperTypeMapExtensions.SetPrefixInsensitiveMap(kv.Key);
            }

            _modelBuilt = true;
        }
    }

    public EntityMapping GetEntityMapping<TEntity>() where TEntity : class
    {
        return GetEntityMapping(typeof(TEntity));
    }

    public EntityMapping GetEntityMapping(Type entityType)
    {
        EnsureModelBuilt();

        if (!_registeredEntityTypes.Contains(entityType))
        {
            throw new DapperConfigurationException(
                entityType.Name,
                $"Entity is not registered in this context. " +
                $"Declare it as a public DapperSet<{entityType.Name}> property on '{_contextType.Name}'.");
        }

        if (_model.TryGetValue(entityType, out var mapping))
            return mapping;

        throw new DapperConfigurationException(
            entityType.Name,
            "Mapping was not built. Ensure it is registered in the model.");
    }

    private void InitializeMappingsFromAttributes(DapperModelBuilder builder)
    {
        foreach (var entityType in GetDbSetEntityTypes())
        {
            builder.Entity(entityType);
        }
    }

    private void RegisterDbSetEntityTypes()
    {
        foreach (var t in GetDbSetEntityTypes())
        {
            _registeredEntityTypes.Add(t);
        }
    }

    private IEnumerable<Type> GetDbSetEntityTypes()
    {
        return _contextType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p =>
                p.PropertyType.IsGenericType &&
                p.PropertyType.GetGenericTypeDefinition() == typeof(DapperSet<>))
            .Select(p => p.PropertyType.GetGenericArguments()[0]);
    }

    private void ApplyDbSetNameConvention(DapperModelBuilder builder)
    {
        var props = _contextType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p =>
                p.PropertyType.IsGenericType &&
                p.PropertyType.GetGenericTypeDefinition() == typeof(DapperSet<>));

        foreach (var prop in props)
        {
            var entityType = prop.PropertyType.GetGenericArguments()[0];

            builder.Entity(entityType, b =>
            {
                if (string.IsNullOrWhiteSpace(b.TableName))
                {
                    b.ToTable(prop.Name, builder.DefaultSchema);
                }
            });
        }
    }
}



