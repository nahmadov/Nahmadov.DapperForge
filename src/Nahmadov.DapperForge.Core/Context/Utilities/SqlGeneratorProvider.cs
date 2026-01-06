using System.Collections.Concurrent;

using Nahmadov.DapperForge.Core.Builders;
using Nahmadov.DapperForge.Core.Exceptions;
using Nahmadov.DapperForge.Core.Interfaces;

namespace Nahmadov.DapperForge.Core.Context.Utilities;

/// <summary>
/// Provides cached <see cref="SqlGenerator{TEntity}"/> instances per entity type.
/// </summary>
internal sealed class SqlGeneratorProvider
{
    private readonly ISqlDialect _dialect;
    private readonly ContextModelManager _modelManager;
    private readonly ConcurrentDictionary<Type, object> _sqlGeneratorCache = new();

    public SqlGeneratorProvider(ISqlDialect dialect, ContextModelManager modelManager)
    {
        _dialect = dialect;
        _modelManager = modelManager;
    }

    public SqlGenerator<TEntity> GetGenerator<TEntity>() where TEntity : class
    {
        return (SqlGenerator<TEntity>)GetGenerator(typeof(TEntity));
    }

    public object GetGenerator(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        _modelManager.EnsureModelBuilt();

        return _sqlGeneratorCache.GetOrAdd(entityType, t =>
        {
            var mapping = _modelManager.GetEntityMapping(t);

            var genType = typeof(SqlGenerator<>).MakeGenericType(t);
            return Activator.CreateInstance(genType, _dialect, mapping)
                ?? throw new DapperConfigurationException(
                    t.Name,
                    "Could not create SqlGenerator. This is likely an internal error.");
        });
    }
}
