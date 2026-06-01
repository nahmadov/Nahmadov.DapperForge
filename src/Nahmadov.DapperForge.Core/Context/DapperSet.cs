using System.Data;
using System.Linq.Expressions;

using Nahmadov.DapperForge.Core.Mutations.Execution;
using Nahmadov.DapperForge.Core.Mutations.Bulk;
using Nahmadov.DapperForge.Core.Querying.Execution;
using Nahmadov.DapperForge.Core.Querying.Sql;
using Nahmadov.DapperForge.Core.Infrastructure.Exceptions;
using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;
using Nahmadov.DapperForge.Core.Schema;

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

    #region Temp Tables

    /// <summary>
    /// Creates a session temp table whose shape mirrors this entity, using the same column names and
    /// types DapperForge resolves for the entity. Database-generated / identity columns are excluded.
    /// </summary>
    /// <param name="name">The temp-table name (the dialect normalises it).</param>
    /// <param name="connection">An open connection to create the temp table on.</param>
    /// <param name="ct">A cancellation token.</param>
    public Task CreateTempTableLikeAsync(string name, IDbConnection connection, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var builder = _context.TempTable(name);
        EntityTempTableSchema.Populate(builder, _mapping);
        return builder.CreateAsync(connection, ct);
    }

    /// <summary>
    /// Creates a session temp table mirroring only the chosen columns of this entity, in the given
    /// order. Explicitly chosen columns are emitted even if they are database-generated.
    /// </summary>
    /// <param name="name">The temp-table name (the dialect normalises it).</param>
    /// <param name="connection">An open connection to create the temp table on.</param>
    /// <param name="columns">Property selectors choosing which columns to include.</param>
    public Task CreateTempTableLikeAsync(
        string name,
        IDbConnection connection,
        params Expression<Func<TEntity, object?>>[] columns)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var builder = _context.TempTable(name);
        var selected = columns is { Length: > 0 }
            ? columns.Select(GetPropertyName).ToList()
            : null;
        EntityTempTableSchema.Populate(builder, _mapping, selected);
        return builder.CreateAsync(connection);
    }

    private static string GetPropertyName(Expression<Func<TEntity, object?>> expr)
    {
        if (expr.Body is MemberExpression m)
            return m.Member.Name;
        if (expr.Body is UnaryExpression u && u.Operand is MemberExpression m2)
            return m2.Member.Name;

        throw new InvalidOperationException(
            "Only simple property expressions (e => e.Property) are supported for column selection.");
    }

    #endregion

    #region Bulk Copy

    /// <summary>
    /// Bulk-copies entities into a destination table (e.g. a temp table created by
    /// <see cref="CreateTempTableLikeAsync(string, IDbConnection, CancellationToken)"/>). Column
    /// mappings are derived from this entity's mapping, so the source columns match the temp-table
    /// shape. SQL Server uses <c>SqlBulkCopy</c>; SQLite uses a batched-insert fallback.
    /// </summary>
    /// <param name="rows">The entities to copy.</param>
    /// <param name="destinationTable">The destination table name.</param>
    /// <param name="connection">An open connection (temp tables are connection-scoped).</param>
    /// <param name="options">Optional bulk-copy options.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The number of rows copied.</returns>
    /// <exception cref="DapperConfigurationException">The dialect does not support bulk copy.</exception>
    public Task<int> BulkCopyAsync(
        IEnumerable<TEntity> rows,
        string destinationTable,
        IDbConnection connection,
        BulkCopyOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(connection);

        var executor = CreateBulkCopyExecutor();
        var dataTable = EntityDataTableFactory.ToDataTable(_mapping, rows);
        return executor.BulkCopyAsync(connection, destinationTable, dataTable, options ?? new BulkCopyOptions(), ct);
    }

    private IBulkCopyExecutor CreateBulkCopyExecutor()
    {
        if (!_generator.Dialect.SupportsBulkCopy)
        {
            throw new DapperConfigurationException(
                typeof(TEntity).Name,
                $"Dialect '{_generator.DialectName}' does not support bulk copy.");
        }

        return _generator.Dialect.CreateBulkCopyExecutor();
    }

    #endregion
}

