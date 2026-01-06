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
/// <typeparam name="TEntity">
/// The entity type. Must be a reference type (class constraint) to ensure entities can be null
/// and to support identity comparison during Include operations.
/// </typeparam>
/// <remarks>
/// <para><b>Performance Characteristics:</b></para>
/// <list type="bullet">
/// <item>All query methods execute immediately without change tracking overhead</item>
/// <item>Expression predicates are cached to avoid recompilation (max 1000 cached expressions)</item>
/// <item>Include operations use identity cache (max 10,000 entities per query) with LRU eviction</item>
/// </list>
/// </remarks>
public sealed class DapperSet<TEntity> where TEntity : class
{
    private readonly DapperDbContext _context;
    private readonly SqlGenerator<TEntity> _generator;
    private readonly EntityMapping _mapping;
    private readonly EntityQueryExecutor<TEntity> _queryExecutor;
    private readonly EntityMutationExecutor<TEntity> _mutationExecutor;

    /// <summary>
    /// Initializes a new <see cref="DapperSet{TEntity}"/> instance.
    /// </summary>
    /// <param name="context">Owning database context.</param>
    /// <param name="generator">SQL generator for the entity.</param>
    /// <param name="mapping">Mapping metadata.</param>
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
    /// <returns>IDapperQueryable for building the query.</returns>
    public IDapperQueryable<TEntity> Query()
        => new DapperQueryable<TEntity>(_context, _generator, _mapping);

    /// <summary>
    /// Retrieves all rows for the entity.
    /// </summary>
    /// <returns>All entity instances from the database.</returns>
    /// <exception cref="DapperConnectionException">Thrown when database connection fails.</exception>
    /// <exception cref="DapperExecutionException">Thrown when SQL execution fails (wrapped from underlying exceptions).</exception>
    /// <remarks>
    /// <b>Performance:</b> This method fetches ALL rows. For large tables, use <see cref="WhereAsync"/>
    /// or raw SQL with pagination to avoid memory issues.
    /// </remarks>
    public Task<IEnumerable<TEntity>> GetAllAsync()
        => _queryExecutor.GetAllAsync();

    /// <summary>
    /// Finds an entity by key value.
    /// </summary>
    /// <param name="key">
    /// Key value (for single-column keys) or composite key object with matching property names.
    /// For composite keys, use an object with properties matching key column names or a Dictionary&lt;string, object&gt;.
    /// </param>
    /// <returns>The matching entity or null if not found.</returns>
    /// <exception cref="DapperConfigurationException">
    /// Thrown when entity has no key configured or FindAsync is not properly configured.
    /// </exception>
    /// <exception cref="DapperConnectionException">Thrown when database connection fails.</exception>
    /// <exception cref="DapperExecutionException">Thrown when SQL execution fails.</exception>
    /// <remarks>
    /// <b>Performance:</b> Uses indexed primary key lookup. Very fast (typically &lt;1ms for indexed keys).
    /// </remarks>
    public Task<TEntity?> FindAsync(object key)
        => _queryExecutor.FindAsync(key);

    /// <summary>
    /// Executes a filtered query using the specified predicate.
    /// </summary>
    /// <param name="predicate">
    /// Predicate expression to translate to SQL WHERE clause.
    /// Supports: ==, !=, &gt;, &gt;=, &lt;, &lt;=, Contains, StartsWith, EndsWith, logical operators (&amp;&amp;, ||, !), and collection.Contains() -&gt; IN.
    /// </param>
    /// <param name="ignoreCase">
    /// When true, applies LOWER() to both sides of string comparisons for case-insensitive matching.
    /// Recommended for cross-database compatibility (SQL Server collation-dependent, Oracle case-sensitive).
    /// </param>
    /// <returns>Enumerable of matching entities.</returns>
    /// <exception cref="ArgumentNullException">Thrown when predicate is null.</exception>
    /// <exception cref="NotSupportedException">Thrown when predicate contains unsupported expressions.</exception>
    /// <exception cref="DapperConnectionException">Thrown when database connection fails.</exception>
    /// <exception cref="DapperExecutionException">Thrown when SQL execution fails.</exception>
    /// <remarks>
    /// <b>Performance:</b> Expression compilation is cached (max 1000 entries).
    /// Subsequent calls with the same expression structure reuse cached compiled code.
    /// SQL is parameterized to prevent injection and enable query plan caching.
    /// </remarks>
    public Task<IEnumerable<TEntity>> WhereAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false)
        => _queryExecutor.WhereAsync(predicate, ignoreCase);

    /// <summary>
    /// Returns the first entity matching the predicate.
    /// Throws if the sequence is empty.
    /// </summary>
    /// <param name="predicate">Predicate expression to translate to SQL WHERE clause.</param>
    /// <param name="ignoreCase">When true, uses case-insensitive comparison where supported.</param>
    /// <returns>The first matching entity.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no entities match the predicate (sequence contains no elements).
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when predicate is null.</exception>
    /// <exception cref="NotSupportedException">Thrown when predicate contains unsupported expressions.</exception>
    /// <exception cref="DapperConnectionException">Thrown when database connection fails.</exception>
    /// <exception cref="DapperExecutionException">Thrown when SQL execution fails.</exception>
    /// <remarks>
    /// <b>Performance:</b> Stops at first match. More efficient than fetching all rows and filtering client-side.
    /// </remarks>
    public async Task<TEntity> FirstAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false)
        => await _queryExecutor.FirstAsync(predicate, ignoreCase).ConfigureAwait(false);

    /// <summary>
    /// Returns the first entity matching the predicate or null if none are found.
    /// </summary>
    /// <param name="predicate">Predicate expression to translate.</param>
    /// <param name="ignoreCase">When true, uses case-insensitive comparison where supported.</param>
    public Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false)
        => _queryExecutor.FirstOrDefaultAsync(predicate, ignoreCase);

    /// <summary>
    /// Determines whether any entities match the specified predicate.
    /// </summary>
    /// <param name="predicate">Predicate expression to translate.</param>
    /// <param name="ignoreCase">When true, uses case-insensitive comparison where supported.</param>
    public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false)
        => await _queryExecutor.AnyAsync(predicate, ignoreCase).ConfigureAwait(false);

    /// <summary>
    /// Determines whether all entities match the specified predicate.
    /// </summary>
    /// <param name="predicate">Predicate expression to translate.</param>
    /// <param name="ignoreCase">When true, uses case-insensitive comparison where supported.</param>
    public async Task<bool> AllAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false)
        => await _queryExecutor.AllAsync(predicate, ignoreCase).ConfigureAwait(false);

    /// <summary>
    /// Returns the count of entities matching the specified predicate.
    /// </summary>
    /// <param name="predicate">Predicate expression to translate.</param>
    /// <param name="ignoreCase">When true, uses case-insensitive comparison where supported.</param>
    public Task<long> CountAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false)
        => _queryExecutor.CountAsync(predicate, ignoreCase);

    #endregion

    #region Insert / Update / Delete

    /// <summary>
    /// Inserts a new entity and returns affected row count.
    /// Executes immediately (no change tracking or SaveChanges required).
    /// </summary>
    /// <param name="entity">Entity to insert. Must not be null.</param>
    /// <param name="transaction">Optional transaction for the operation. If null, auto-commits.</param>
    /// <returns>Number of affected rows (typically 1 on success).</returns>
    /// <exception cref="ArgumentNullException">Thrown when entity is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when entity is marked as ReadOnly or has no key configured.
    /// </exception>
    /// <exception cref="DapperValidationException">
    /// Thrown when entity fails validation (required fields missing, string length exceeded, etc.).
    /// </exception>
    /// <exception cref="DapperConfigurationException">
    /// Thrown when Insert SQL is not configured for the entity.
    /// </exception>
    /// <exception cref="DapperExecutionException">
    /// Thrown when SQL execution fails (wrapped from database exceptions like constraint violations, etc.).
    /// </exception>
    /// <remarks>
    /// <b>Performance:</b> Immediate execution. No batching or change tracking overhead.
    /// Auto-generated columns (identity, computed) are skipped from INSERT statement.
    /// </remarks>
    public async Task<int> InsertAsync(TEntity entity, IDbTransaction? transaction = null)
        => await _mutationExecutor.InsertAsync(entity, transaction).ConfigureAwait(false);

    /// <summary>
    /// Updates an existing entity and throws if no rows are affected.
    /// Executes immediately (no change tracking or SaveChanges required).
    /// </summary>
    /// <param name="entity">Entity to update. Must not be null and must have valid key values.</param>
    /// <param name="transaction">Optional transaction for the operation. If null, auto-commits.</param>
    /// <returns>Number of affected rows (typically 1 on success).</returns>
    /// <exception cref="ArgumentNullException">Thrown when entity is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when entity is marked as ReadOnly or has no key configured.
    /// </exception>
    /// <exception cref="DapperValidationException">
    /// Thrown when entity fails validation (required fields missing, string length exceeded, etc.).
    /// </exception>
    /// <exception cref="DapperConfigurationException">
    /// Thrown when Update SQL is not configured or no columns are updatable.
    /// </exception>
    /// <exception cref="DapperConcurrencyException">
    /// Thrown when no rows are affected (entity not found or optimistic concurrency violation).
    /// </exception>
    /// <exception cref="DapperExecutionException">
    /// Thrown when SQL execution fails (wrapped from database exceptions).
    /// </exception>
    /// <remarks>
    /// <b>Performance:</b> Immediate execution. No change tracking overhead.
    /// Read-only and generated columns are excluded from UPDATE statement.
    /// </remarks>
    public async Task<int> UpdateAsync(TEntity entity, IDbTransaction? transaction = null)
        => await _mutationExecutor.UpdateAsync(entity, transaction).ConfigureAwait(false);

    /// <summary>
    /// Deletes an entity using its key values.
    /// </summary>
    /// <param name="entity">Entity to delete.</param>
    /// <param name="transaction">Optional transaction for the operation.</param>
    public async Task<int> DeleteAsync(TEntity entity, IDbTransaction? transaction = null)
        => await _mutationExecutor.DeleteAsync(entity, transaction).ConfigureAwait(false);

    /// <summary>
    /// Deletes an entity by key value.
    /// </summary>
    /// <param name="key">Key value or composite key object.</param>
    /// <param name="transaction">Optional transaction for the operation.</param>
    public async Task<int> DeleteByIdAsync(object key, IDbTransaction? transaction = null)
        => await _mutationExecutor.DeleteByIdAsync(key, transaction).ConfigureAwait(false);

    /// <summary>
    /// Inserts a new entity and returns the generated key value.
    /// Works with auto-generated identity/sequence columns.
    /// </summary>
    /// <typeparam name="TKey">
    /// Key value type. Must match the entity's key property type (typically int, long, or Guid).
    /// Type conversion is attempted if types don't match exactly.
    /// </typeparam>
    /// <param name="entity">Entity to insert. Key property will be populated with generated value.</param>
    /// <param name="transaction">Optional transaction for the operation. If null, auto-commits.</param>
    /// <returns>The generated key value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when entity is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when entity is marked as ReadOnly or has no key configured.
    /// </exception>
    /// <exception cref="DapperValidationException">
    /// Thrown when entity fails validation.
    /// </exception>
    /// <exception cref="DapperConfigurationException">
    /// Thrown when entity has composite keys (only single keys supported),
    /// key is not auto-generated, or dialect doesn't support RETURNING clause.
    /// </exception>
    /// <exception cref="DapperOperationException">
    /// Thrown when database returns NULL or default value (insert failed silently, trigger issues, etc.).
    /// </exception>
    /// <exception cref="DapperKeyAssignmentException">
    /// Thrown when generated key cannot be assigned to entity's key property (type mismatch).
    /// </exception>
    /// <exception cref="DapperExecutionException">
    /// Thrown when SQL execution fails.
    /// </exception>
    /// <remarks>
    /// <para><b>Dialect Support:</b></para>
    /// <list type="bullet">
    /// <item>SQL Server: Uses OUTPUT INSERTED.Id (full support)</item>
    /// <item>Oracle: Uses RETURNING INTO with output parameters (full support in v1)</item>
    /// </list>
    /// <para><b>Performance:</b> Single round-trip. Key is automatically assigned to entity's key property.</para>
    /// </remarks>
    public async Task<TKey> InsertAndGetIdAsync<TKey>(TEntity entity, IDbTransaction? transaction = null)
        => await _mutationExecutor.InsertAndGetIdAsync<TKey>(entity, transaction).ConfigureAwait(false);

    /// <summary>
    /// Updates an entity using explicit WHERE conditions instead of primary/alternate key.
    /// Provides control over affected row count and supports mass operations.
    /// </summary>
    /// <param name="entity">Entity with updated values.</param>
    /// <param name="where">
    /// WHERE conditions as anonymous object with property-value pairs.
    /// All properties must correspond to mapped entity columns.
    /// Example: new { Status = "Active", Department = "IT" }
    /// </param>
    /// <param name="allowMultiple">
    /// Set to true to allow multiple rows to be affected.
    /// Default is false (expects exactly 1 row). Prevents accidental mass updates.
    /// </param>
    /// <param name="expectedRows">
    /// Expected number of affected rows. If specified, throws if actual count differs.
    /// Takes precedence over allowMultiple when set.
    /// </param>
    /// <param name="transaction">Optional transaction for the operation.</param>
    /// <returns>Number of affected rows.</returns>
    /// <exception cref="ArgumentNullException">Thrown when entity or where is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when entity is marked as ReadOnly or has no key configured.
    /// </exception>
    /// <exception cref="DapperConfigurationException">
    /// Thrown when WHERE column is not found in entity mapping or Update SQL is not configured.
    /// </exception>
    /// <exception cref="DapperOperationException">
    /// Thrown when affected row count doesn't match expectations (0 rows, multiple rows when allowMultiple=false, etc.).
    /// </exception>
    /// <exception cref="DapperConcurrencyException">
    /// Thrown when 0 rows are affected and allowMultiple=false (entity not found).
    /// </exception>
    /// <exception cref="DapperExecutionException">
    /// Thrown when SQL execution fails.
    /// </exception>
    /// <remarks>
    /// <para><b>Safety Features:</b></para>
    /// <list type="bullet">
    /// <item>All WHERE columns are validated against entity mapping (prevents SQL injection)</item>
    /// <item>WHERE conditions are fully parameterized</item>
    /// <item>Patterns like WHERE 1=1 are explicitly forbidden</item>
    /// <item>By default, expects exactly 1 row affected (prevents accidental mass updates)</item>
    /// <item>Null values are handled as "IS NULL" instead of "= NULL"</item>
    /// </list>
    /// <para><b>Example Usage:</b></para>
    /// <code>
    /// await employees.UpdateAsync(
    ///     new Employee { Salary = 75000 },
    ///     new { EmployeeNumber = "EMP-12345" });
    ///
    /// await employees.UpdateAsync(
    ///     new Employee { Status = "Inactive" },
    ///     new { Department = "IT", Location = "Seattle" },
    ///     allowMultiple: true);
    ///
    /// await employees.UpdateAsync(
    ///     new Employee { BonusPercent = 10 },
    ///     new { PerformanceRating = "Excellent" },
    ///     expectedRows: 5);
    /// </code>
    /// </remarks>
    public async Task<int> UpdateAsync(TEntity entity, object where, bool allowMultiple = false, int? expectedRows = null, IDbTransaction? transaction = null)
        => await _mutationExecutor.UpdateAsync(entity, where, allowMultiple, expectedRows, transaction).ConfigureAwait(false);

    /// <summary>
    /// Deletes entities using explicit WHERE conditions instead of primary/alternate key.
    /// Provides control over affected row count and supports mass operations.
    /// </summary>
    /// <param name="where">
    /// WHERE conditions as anonymous object with property-value pairs.
    /// All properties must correspond to mapped entity columns.
    /// Example: new { Status = "Deleted", LastModified = someDate }
    /// </param>
    /// <param name="allowMultiple">
    /// Set to true to allow multiple rows to be deleted.
    /// Default is false (expects exactly 1 row). Prevents accidental mass deletes.
    /// </param>
    /// <param name="expectedRows">
    /// Expected number of affected rows. If specified, throws if actual count differs.
    /// Takes precedence over allowMultiple when set.
    /// </param>
    /// <param name="transaction">Optional transaction for the operation.</param>
    /// <returns>Number of affected rows.</returns>
    /// <exception cref="ArgumentNullException">Thrown when where is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when entity is marked as ReadOnly or has no key configured.
    /// </exception>
    /// <exception cref="DapperConfigurationException">
    /// Thrown when WHERE column is not found in entity mapping.
    /// </exception>
    /// <exception cref="DapperOperationException">
    /// Thrown when affected row count doesn't match expectations (0 rows, multiple rows when allowMultiple=false, etc.).
    /// </exception>
    /// <exception cref="DapperConcurrencyException">
    /// Thrown when 0 rows are affected and allowMultiple=false (no matching entities found).
    /// </exception>
    /// <exception cref="DapperExecutionException">
    /// Thrown when SQL execution fails.
    /// </exception>
    /// <remarks>
    /// <para><b>Safety Features:</b></para>
    /// <list type="bullet">
    /// <item>All WHERE columns are validated against entity mapping (prevents SQL injection)</item>
    /// <item>WHERE conditions are fully parameterized</item>
    /// <item>Patterns like WHERE 1=1 are explicitly forbidden</item>
    /// <item>By default, expects exactly 1 row affected (prevents accidental mass deletes)</item>
    /// <item>Null values are handled as "IS NULL" instead of "= NULL"</item>
    /// </list>
    /// <para><b>Example Usage:</b></para>
    /// <code>
    /// await employees.DeleteAsync(new { EmployeeNumber = "EMP-12345" });
    ///
    /// await employees.DeleteAsync(
    ///     new { Status = "Inactive", Department = "IT" },
    ///     allowMultiple: true);
    ///
    /// await employees.DeleteAsync(
    ///     new { IsTemporary = true, EndDate = someDate },
    ///     expectedRows: 3);
    /// </code>
    /// </remarks>
    public async Task<int> DeleteAsync(object where, bool allowMultiple = false, int? expectedRows = null, IDbTransaction? transaction = null)
        => await _mutationExecutor.DeleteAsync(where, allowMultiple, expectedRows, transaction).ConfigureAwait(false);

    #endregion
}