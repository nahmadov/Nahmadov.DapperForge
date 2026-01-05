using System.Data;
using System.Linq.Expressions;
using System.Reflection;

using Dapper;

using Nahmadov.DapperForge.Core.Builders;
using Nahmadov.DapperForge.Core.Exceptions;
using Nahmadov.DapperForge.Core.Interfaces;
using Nahmadov.DapperForge.Core.Mapping;
using Nahmadov.DapperForge.Core.Validation;

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
        => _context.QueryAsync<TEntity>(_generator.SelectAllSql);

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

        var param = BuildKeyParameters(key);

        return _context.QueryFirstOrDefaultAsync<TEntity>(_generator.SelectByIdSql, param);
    }

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
    {
        var dialect = _generator.Dialect;
        var visitor = new PredicateVisitor<TEntity>(_mapping, dialect);
        var (sql, parameters) = visitor.Translate(predicate, ignoreCase);

        var finalSql = $"{_generator.SelectAllSql} WHERE {sql}";
        return _context.QueryAsync<TEntity>(finalSql, parameters);
    }

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
    {
        var result = await FirstOrDefaultAsync(predicate, ignoreCase).ConfigureAwait(false) ?? throw new InvalidOperationException("Sequence contains no elements.");
        return result;
    }

    /// <summary>
    /// Returns the first entity matching the predicate or null if none are found.
    /// </summary>
    /// <param name="predicate">Predicate expression to translate.</param>
    /// <param name="ignoreCase">When true, uses case-insensitive comparison where supported.</param>
    public Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false)
    {
        var dialect = _generator.Dialect;
        var visitor = new PredicateVisitor<TEntity>(_mapping, dialect);
        var (sql, parameters) = visitor.Translate(predicate, ignoreCase);

        var finalSql = $"{_generator.SelectAllSql} WHERE {sql}";
        return _context.QueryFirstOrDefaultAsync<TEntity>(finalSql, parameters);
    }

    /// <summary>
    /// Determines whether any entities match the specified predicate.
    /// </summary>
    /// <param name="predicate">Predicate expression to translate.</param>
    /// <param name="ignoreCase">When true, uses case-insensitive comparison where supported.</param>
    public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false)
    {
        var count = await CountAsync(predicate, ignoreCase).ConfigureAwait(false);
        return count > 0;
    }

    /// <summary>
    /// Determines whether all entities match the specified predicate.
    /// </summary>
    /// <param name="predicate">Predicate expression to translate.</param>
    /// <param name="ignoreCase">When true, uses case-insensitive comparison where supported.</param>
    public async Task<bool> AllAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false)
    {
        var visitor = new PredicateVisitor<TEntity>(_mapping, _generator.Dialect);
        var (whereClause, parameters) = visitor.Translate(predicate, ignoreCase);

        var countSql = $"SELECT COUNT(*) FROM {_generator.TableName} AS a WHERE NOT ({whereClause})";
        var countNotMatching = await _context.QueryFirstOrDefaultAsync<long>(countSql, parameters).ConfigureAwait(false);

        return countNotMatching == 0;
    }

    /// <summary>
    /// Returns the count of entities matching the specified predicate.
    /// </summary>
    /// <param name="predicate">Predicate expression to translate.</param>
    /// <param name="ignoreCase">When true, uses case-insensitive comparison where supported.</param>
    public Task<long> CountAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false)
    {
        var visitor = new PredicateVisitor<TEntity>(_mapping, _generator.Dialect);
        var (whereClause, parameters) = visitor.Translate(predicate, ignoreCase);

        var countSql = $"SELECT COUNT(*) FROM {_generator.TableName} AS a WHERE {whereClause}";
        return _context.QueryFirstOrDefaultAsync<long>(countSql, parameters);
    }

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
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureCanMutate();
        EntityValidator<TEntity>.ValidateForInsert(entity, _mapping);

        if (string.IsNullOrWhiteSpace(_generator.InsertSql))
        {
            throw new DapperConfigurationException(
                typeof(TEntity).Name,
                "Insert SQL is not configured.");
        }

        try
        {
            return await _context.ExecuteAsync(_generator.InsertSql, entity, transaction).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not DapperForgeException)
        {
            throw new DapperExecutionException(
                OperationType.Insert,
                typeof(TEntity).Name,
                _generator.InsertSql,
                ex);
        }
    }

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
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureCanMutate();
        EntityValidator<TEntity>.ValidateForUpdate(entity, _mapping);

        if (string.IsNullOrWhiteSpace(_generator.UpdateSql))
        {
            throw new DapperConfigurationException(
                typeof(TEntity).Name,
                "Update SQL is not configured or no columns are updatable.");
        }

        int affected;
        try
        {
            affected = await _context.ExecuteAsync(_generator.UpdateSql, entity, transaction).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not DapperForgeException)
        {
            throw new DapperExecutionException(
                OperationType.Update,
                typeof(TEntity).Name,
                _generator.UpdateSql,
                ex);
        }

        if (affected == 0)
        {
            throw new DapperConcurrencyException(OperationType.Update, typeof(TEntity).Name);
        }

        return affected;
    }

    /// <summary>
    /// Deletes an entity using its key values.
    /// </summary>
    /// <param name="entity">Entity to delete.</param>
    /// <param name="transaction">Optional transaction for the operation.</param>
    public async Task<int> DeleteAsync(TEntity entity, IDbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureCanMutate();

        if (string.IsNullOrWhiteSpace(_generator.DeleteByIdSql))
        {
            throw new DapperConfigurationException(
                typeof(TEntity).Name,
                "Delete SQL is not configured.");
        }

        int affected;
        try
        {
            affected = await _context.ExecuteAsync(_generator.DeleteByIdSql, entity, transaction).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not DapperForgeException)
        {
            throw new DapperExecutionException(
                OperationType.Delete,
                typeof(TEntity).Name,
                _generator.DeleteByIdSql,
                ex);
        }

        if (affected == 0)
        {
            throw new DapperConcurrencyException(OperationType.Delete, typeof(TEntity).Name);
        }

        return affected;
    }

    /// <summary>
    /// Deletes an entity by key value.
    /// </summary>
    /// <param name="key">Key value or composite key object.</param>
    /// <param name="transaction">Optional transaction for the operation.</param>
    public async Task<int> DeleteByIdAsync(object key, IDbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        EnsureCanMutate();

        if (_mapping.KeyProperties.Count == 0)
        {
            throw new DapperConfigurationException(
                typeof(TEntity).Name,
                "Entity has no key and cannot be deleted by id.");
        }

        if (string.IsNullOrWhiteSpace(_generator.DeleteByIdSql))
        {
            throw new DapperConfigurationException(
                typeof(TEntity).Name,
                "Delete SQL is not configured.");
        }

        var param = BuildKeyParameters(key);

        int affected;
        try
        {
            affected = await _context.ExecuteAsync(_generator.DeleteByIdSql, param, transaction).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not DapperForgeException)
        {
            throw new DapperExecutionException(
                OperationType.Delete,
                typeof(TEntity).Name,
                _generator.DeleteByIdSql,
                ex);
        }

        if (affected == 0)
        {
            throw new DapperConcurrencyException(OperationType.Delete, typeof(TEntity).Name);
        }

        return affected;
    }

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
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureCanMutate();
        EntityValidator<TEntity>.ValidateForInsert(entity, _mapping);

        if (_mapping.KeyProperties.Count != 1)
        {
            throw new DapperConfigurationException(
                typeof(TEntity).Name,
                "InsertAndGetIdAsync requires a single key property.");
        }

        if (!_generator.IsKeyGenerated)
        {
            var keyPropGenerated = _generator.KeyProperty
                ?? throw new DapperConfigurationException(
                    typeof(TEntity).Name,
                    "InsertAndGetIdAsync requires a key property.");

            var value = keyPropGenerated.GetValue(entity);
            if (value is TKey typed)
                return typed;

            throw new DapperConfigurationException(
                typeof(TEntity).Name,
                $"Key '{keyPropGenerated.Name}' is not generated by the database. " +
                "Use InsertAsync and retrieve the key from the entity instance.");
        }

        if (_generator.InsertReturningIdSql is null)
        {
            throw new DapperConfigurationException(
                typeof(TEntity).Name,
                $"Dialect '{_generator.DialectName}' does not support InsertAndGetIdAsync. " +
                "Use InsertAsync and set the key manually or extend the dialect.");
        }

        TKey? id;
        try
        {
            if (string.Equals(_generator.DialectName, "Oracle", StringComparison.OrdinalIgnoreCase))
            {
                id = await ExecuteOracleInsertReturningAsync<TKey>(entity, transaction).ConfigureAwait(false);
            }
            else
            {
                id = await _context.QueryFirstOrDefaultAsync<TKey>(_generator.InsertReturningIdSql, entity, transaction).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not DapperForgeException)
        {
            throw new DapperExecutionException(
                OperationType.Insert,
                typeof(TEntity).Name,
                _generator.InsertReturningIdSql,
                ex);
        }

        if (id == null || EqualityComparer<TKey>.Default.Equals(id, default!))
        {
            throw new DapperOperationException(
                OperationType.Insert,
                typeof(TEntity).Name,
                "Identity value returned NULL or default. " +
                "This may indicate: (1) insert failed silently, (2) the provider cannot return identity, " +
                "(3) a trigger suppressed the identity value, or (4) SqlDialect needs adjustments.");
        }

        AssignKeyToEntity(entity, id);

        return id;
    }

    #endregion

    /// <summary>
    /// Attempts to assign a generated key value to the entity.
    /// Throws <see cref="DapperKeyAssignmentException"/> if assignment fails.
    /// </summary>
    /// <typeparam name="TKey">Type of the key value.</typeparam>
    /// <param name="entity">Entity to assign the key to.</param>
    /// <param name="keyValue">Generated key value.</param>
    private void AssignKeyToEntity<TKey>(TEntity entity, TKey keyValue)
    {
        var keyProp = _generator.KeyProperty;
        if (keyProp is null)
            return;

        try
        {
            var targetType = keyProp.PropertyType;
            object? converted = keyValue;

            if (!targetType.IsAssignableFrom(typeof(TKey)))
            {
                converted = Convert.ChangeType(keyValue, targetType);
            }

            keyProp.SetValue(entity, converted);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException or TargetException)
        {
            throw new DapperKeyAssignmentException(
                entityName: typeof(TEntity).Name,
                keyPropertyName: keyProp.Name,
                keyValue: keyValue,
                innerException: ex);
        }
    }

    /// <summary>
    /// Validates that the entity supports mutation operations.
    /// </summary>
    private void EnsureCanMutate()
    {
        if (_mapping.IsReadOnly)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' is marked as ReadOnly and cannot be modified.");
        }

        if (_mapping.KeyProperties.Count == 0)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' has no key and cannot be updated/deleted.");
        }
    }

    /// <summary>
    /// Builds a parameter dictionary for key columns from various key representations.
    /// </summary>
    /// <param name="key">Key value, composite object, or dictionary.</param>
    /// <returns>Dictionary mapping key property names to values.</returns>
    private Dictionary<string, object?> BuildKeyParameters(object key)
    {
        if (_mapping.KeyProperties.Count == 1)
        {
            return new Dictionary<string, object?>
            {
                [_mapping.KeyProperties[0].Name] = key
            };
        }

        if (key is IDictionary<string, object?> dict)
        {
            var result = new Dictionary<string, object?>();
            foreach (var kp in _mapping.KeyProperties)
            {
                if (!dict.TryGetValue(kp.Name, out var value))
                {
                    throw new InvalidOperationException(
                        $"Key parameter missing value for '{kp.Name}' for entity '{typeof(TEntity).Name}'.");
                }
                result[kp.Name] = value;
            }
            return result;
        }

        var keyType = key.GetType();
        var resultFromObject = new Dictionary<string, object?>();
        foreach (var kp in _mapping.KeyProperties)
        {
            var prop = keyType.GetProperty(kp.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (prop is null)
            {
                throw new InvalidOperationException(
                    $"Key object does not contain property '{kp.Name}' required for entity '{typeof(TEntity).Name}'.");
            }

            resultFromObject[kp.Name] = prop.GetValue(key);
        }

        return resultFromObject;
    }

    /// <summary>
    /// Executes an Oracle insert using RETURNING INTO and extracts the generated key.
    /// </summary>
    /// <typeparam name="TKey">Key type to return.</typeparam>
    /// <param name="entity">Entity being inserted.</param>
    /// <param name="transaction">Optional transaction for the operation.</param>
    private async Task<TKey> ExecuteOracleInsertReturningAsync<TKey>(TEntity entity, IDbTransaction? transaction = null)
    {
        var keyProp = _generator.KeyProperty
            ?? throw new InvalidOperationException($"Entity '{typeof(TEntity).Name}' has no key property.");

        var parameters = new DynamicParameters(entity);
        var clrType = keyProp.PropertyType;

        if (_generator.Dialect.TryMapDbType(clrType, out var dbType))
        {
            parameters.Add(keyProp.Name, dbType: dbType, direction: ParameterDirection.Output);
        }
        else
        {
            parameters.Add(keyProp.Name, dbType: DbType.Object, direction: ParameterDirection.Output);
        }
        await _context.ExecuteAsync(_generator.InsertReturningIdSql!, parameters, transaction).ConfigureAwait(false);

        return parameters.Get<TKey>(keyProp.Name);
    }
}
