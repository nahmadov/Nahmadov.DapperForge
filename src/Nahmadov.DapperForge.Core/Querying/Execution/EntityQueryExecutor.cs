using System.Linq.Expressions;

using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.Core.Infrastructure.Exceptions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;
using Nahmadov.DapperForge.Core.Querying.Predicates;
using Nahmadov.DapperForge.Core.Querying.Sql;

namespace Nahmadov.DapperForge.Core.Querying.Execution;
/// <summary>
/// Encapsulates read-only operations for a <see cref="DapperSet{TEntity}"/>.
/// </summary>
/// <typeparam name="TEntity">Entity type.</typeparam>
internal sealed class EntityQueryExecutor<TEntity>(DapperDbContext context, SqlGenerator<TEntity> generator, EntityMapping mapping) where TEntity : class
{
    private readonly DapperDbContext _context = context;
    private readonly SqlGenerator<TEntity> _generator = generator;
    private readonly EntityMapping _mapping = mapping;

    public Task<IEnumerable<TEntity>> GetAllAsync()
          => _context.QueryAsync<TEntity>(_generator.SelectAllSql);

    public Task<TEntity?> FindAsync(object key)
    {
        if (_mapping.KeyProperties.Count == 0)
        {
            throw new DapperConfigurationException(
                typeof(TEntity).Name,
                "Entity has no key and does not support FindAsync.");
        }

        if (string.IsNullOrWhiteSpace(_generator.SelectByIdSql))
        {
            throw new DapperConfigurationException(
                typeof(TEntity).Name,
                "FindAsync is not configured. Ensure the entity has a key and a proper mapping.");
        }

        var param = KeyParameterBuilder.Build(_mapping, key, typeof(TEntity).Name);

        return _context.QueryFirstOrDefaultAsync<TEntity>(_generator.SelectByIdSql, param);
    }

    public Task<IEnumerable<TEntity>> WhereAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var visitor = new PredicateVisitor<TEntity>(_mapping, _generator.Dialect);
        var (sql, parameters) = visitor.Translate(predicate, ignoreCase);

        var finalSql = $"{_generator.SelectAllSql} WHERE {sql}";
        return _context.QueryAsync<TEntity>(finalSql, parameters);
    }

    public async Task<TEntity> FirstAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase)
    {
        var result = await FirstOrDefaultAsync(predicate, ignoreCase).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Sequence contains no elements.");

        return result;
    }

    public Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var visitor = new PredicateVisitor<TEntity>(_mapping, _generator.Dialect);
        var (sql, parameters) = visitor.Translate(predicate, ignoreCase);

        var finalSql = $"{_generator.SelectAllSql} WHERE {sql}";
        return _context.QueryFirstOrDefaultAsync<TEntity>(finalSql, parameters);
    }

    public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase)
    {
        var count = await CountAsync(predicate, ignoreCase).ConfigureAwait(false);
        return count > 0;
    }

    public async Task<bool> AllAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var visitor = new PredicateVisitor<TEntity>(_mapping, _generator.Dialect);
        var (whereClause, parameters) = visitor.Translate(predicate, ignoreCase);

        var countSql = $"SELECT COUNT(*) FROM {_generator.TableName} AS a WHERE NOT ({whereClause})";
        var countNotMatching = await _context.QueryFirstOrDefaultAsync<long>(countSql, parameters).ConfigureAwait(false);

        return countNotMatching == 0;
    }

    public Task<long> CountAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var visitor = new PredicateVisitor<TEntity>(_mapping, _generator.Dialect);
        var (whereClause, parameters) = visitor.Translate(predicate, ignoreCase);

        var countSql = $"SELECT COUNT(*) FROM {_generator.TableName} AS a WHERE {whereClause}";
        return _context.QueryFirstOrDefaultAsync<long>(countSql, parameters);
    }
}


