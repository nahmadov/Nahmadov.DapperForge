using System.Data;
using System.Linq.Expressions;

using Nahmadov.DapperForge.Core.Mutations.Execution;
using Nahmadov.DapperForge.Core.Mutations.Bulk;
using Nahmadov.DapperForge.Core.Querying.Execution;
using Nahmadov.DapperForge.Core.Querying.Sql;
using Nahmadov.DapperForge.Core.Infrastructure.Exceptions;
using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;

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
    private BulkMutationExecutor<TEntity>? _bulkExecutor;

    internal DapperSet(DapperDbContext context, SqlGenerator<TEntity> generator, EntityMapping mapping)
    {
        _context = context;
        _generator = generator;
        _mapping = mapping;
        _queryExecutor = new EntityQueryExecutor<TEntity>(context, generator, mapping);
        _mutationExecutor = new EntityMutationExecutor<TEntity>(context, generator, mapping);
    }

    private BulkMutationExecutor<TEntity> BulkExecutor
    {
        get
        {
            if (_bulkExecutor is null)
            {
                if (_generator.Dialect is not IBulkSqlDialect bulkDialect)
                {
                    throw new DapperConfigurationException(
                        typeof(TEntity).Name,
                        $"Dialect '{_generator.DialectName}' does not support bulk operations. " +
                        "Use a dialect that implements IBulkSqlDialect.");
                }
                _bulkExecutor = new BulkMutationExecutor<TEntity>(_context, bulkDialect, _mapping);
            }
            return _bulkExecutor;
        }
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

    #region Bulk Operations

    /// <summary>
    /// Inserts multiple entities in optimized batches.
    /// </summary>
    /// <param name="entities">Entities to insert.</param>
    /// <param name="options">Optional bulk insert configuration.</param>
    /// <param name="transaction">Optional transaction.</param>
    /// <returns>Result containing affected row count and execution statistics.</returns>
    public Task<BulkOperationResult> BulkInsertAsync(
        IEnumerable<TEntity> entities,
        BulkInsertOptions? options = null,
        IDbTransaction? transaction = null)
        => BulkExecutor.BulkInsertAsync(entities, options, transaction);

    /// <summary>
    /// Performs an upsert (insert or update) operation on multiple entities.
    /// Matches on primary/alternate key by default.
    /// </summary>
    /// <param name="entities">Entities to merge.</param>
    /// <param name="options">Optional merge configuration.</param>
    /// <param name="transaction">Optional transaction.</param>
    /// <returns>Result containing affected row count and execution statistics.</returns>
    public Task<BulkMergeResult> BulkMergeAsync(
        IEnumerable<TEntity> entities,
        BulkMergeOptions? options = null,
        IDbTransaction? transaction = null)
        => BulkExecutor.BulkMergeAsync(entities, options, transaction);

    /// <summary>
    /// Performs an upsert operation using custom match columns.
    /// </summary>
    /// <param name="entities">Entities to merge.</param>
    /// <param name="matchColumns">Column names to use for matching existing records.</param>
    /// <param name="transaction">Optional transaction.</param>
    /// <returns>Result containing affected row count and execution statistics.</returns>
    public Task<BulkMergeResult> BulkMergeAsync(
        IEnumerable<TEntity> entities,
        IReadOnlyList<string> matchColumns,
        IDbTransaction? transaction = null)
        => BulkExecutor.BulkMergeAsync(entities,
            new BulkMergeOptions { MatchColumns = matchColumns },
            transaction);

    #endregion
}

