using System.Data;
using System.Linq.Expressions;

using Nahmadov.DapperForge.Core.Builders;
using Nahmadov.DapperForge.Core.Context.Execution.Mutation;
using Nahmadov.DapperForge.Core.Context.Execution.Query;
using Nahmadov.DapperForge.Core.Exceptions;
using Nahmadov.DapperForge.Core.Interfaces;
using Nahmadov.DapperForge.Core.Mapping;

namespace Nahmadov.DapperForge.Core.Context;

/// <summary>
/// Provides query and command operations for a specific entity type.
/// </summary>
public sealed class DapperSet<TEntity> where TEntity : class
{
    private readonly DapperDbContext _context;
    private readonly SqlGenerator<TEntity> _generator;
    private readonly EntityMapping _mapping;
    private readonly EntityQueryExecutor<TEntity> _queryExecutor;
    private readonly EntityMutationExecutor<TEntity> _mutationExecutor;

    internal DapperSet(DapperDbContext context, SqlGenerator<TEntity> generator, EntityMapping mapping)
    {
        _context = context;
        _generator = generator;
        _mapping = mapping;
        _queryExecutor = new EntityQueryExecutor<TEntity>(context, generator, mapping);
        _mutationExecutor = new EntityMutationExecutor<TEntity>(context, generator, mapping);
    }

    #region Query

    /// <summary>
    /// Creates a fluent query builder for chaining Where, OrderBy, Skip, Take operations.
    /// </summary>
    public IDapperQueryable<TEntity> Query()
        => new DapperQueryable<TEntity>(_context, _generator, _mapping);

    /// <summary>
    /// Retrieves all rows for the entity.
    /// </summary>
    public Task<IEnumerable<TEntity>> GetAllAsync()
        => _queryExecutor.GetAllAsync();

    /// <summary>
    /// Finds an entity by key value.
    /// </summary>
    public Task<TEntity?> FindAsync(object key)
        => _queryExecutor.FindAsync(key);

    /// <summary>
    /// Executes a filtered query using the specified predicate expression.
    /// </summary>
    public Task<IEnumerable<TEntity>> WhereAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false)
        => _queryExecutor.WhereAsync(predicate, ignoreCase);

    /// <summary>
    /// Returns the first entity matching the predicate.
    /// </summary>
    public async Task<TEntity> FirstAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false)
        => await _queryExecutor.FirstAsync(predicate, ignoreCase).ConfigureAwait(false);

    /// <summary>
    /// Returns the first entity matching the predicate or null if none are found.
    /// </summary>
    public Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false)
        => _queryExecutor.FirstOrDefaultAsync(predicate, ignoreCase);

    /// <summary>
    /// Determines whether any entities match the specified predicate.
    /// </summary>
    public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false)
        => await _queryExecutor.AnyAsync(predicate, ignoreCase).ConfigureAwait(false);

    /// <summary>
    /// Determines whether all entities match the specified predicate.
    /// </summary>
    public async Task<bool> AllAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false)
        => await _queryExecutor.AllAsync(predicate, ignoreCase).ConfigureAwait(false);

    /// <summary>
    /// Returns the count of entities matching the specified predicate.
    /// </summary>
    public Task<long> CountAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false)
        => _queryExecutor.CountAsync(predicate, ignoreCase);

    #endregion

    #region Insert / Update / Delete

    /// <summary>
    /// Inserts a new entity and returns affected row count.
    /// </summary>
    public async Task<int> InsertAsync(TEntity entity, IDbTransaction? transaction = null)
        => await _mutationExecutor.InsertAsync(entity, transaction).ConfigureAwait(false);

    /// <summary>
    /// Updates an existing entity and returns affected row count.
    /// </summary>
    public async Task<int> UpdateAsync(TEntity entity, IDbTransaction? transaction = null)
        => await _mutationExecutor.UpdateAsync(entity, transaction).ConfigureAwait(false);

    /// <summary>
    /// Deletes an entity using its key values.
    /// </summary>
    public async Task<int> DeleteAsync(TEntity entity, IDbTransaction? transaction = null)
        => await _mutationExecutor.DeleteAsync(entity, transaction).ConfigureAwait(false);

    /// <summary>
    /// Deletes an entity by key value.
    /// </summary>
    public async Task<int> DeleteByIdAsync(object key, IDbTransaction? transaction = null)
        => await _mutationExecutor.DeleteByIdAsync(key, transaction).ConfigureAwait(false);

    /// <summary>
    /// Inserts a new entity and returns the generated key value.
    /// </summary>
    public async Task<TKey> InsertAndGetIdAsync<TKey>(TEntity entity, IDbTransaction? transaction = null)
        => await _mutationExecutor.InsertAndGetIdAsync<TKey>(entity, transaction).ConfigureAwait(false);

    /// <summary>
    /// Updates an entity using explicit WHERE conditions with row count control.
    /// </summary>
    public async Task<int> UpdateAsync(TEntity entity, object where, bool allowMultiple = false, int? expectedRows = null, IDbTransaction? transaction = null)
        => await _mutationExecutor.UpdateAsync(entity, where, allowMultiple, expectedRows, transaction).ConfigureAwait(false);

    /// <summary>
    /// Deletes entities using explicit WHERE conditions with row count control.
    /// </summary>
    public async Task<int> DeleteAsync(object where, bool allowMultiple = false, int? expectedRows = null, IDbTransaction? transaction = null)
        => await _mutationExecutor.DeleteAsync(where, allowMultiple, expectedRows, transaction).ConfigureAwait(false);

    #endregion
}