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
    public Task<IEnumerable<TEntity>> GetAllAsync()
        => _context.QueryAsync<TEntity>(_generator.SelectAllSql);

    /// <summary>
    /// Finds an entity by key value.
    /// </summary>
    /// <param name="key">Key value or composite key object.</param>
    /// <returns>The matching entity or null.</returns>
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
    /// <param name="predicate">Predicate expression to translate.</param>
    /// <param name="ignoreCase">When true, uses case-insensitive comparison where supported.</param>
    /// <returns>Enumerable of matching entities.</returns>
    public Task<IEnumerable<TEntity>> WhereAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false)
    {
        var dialect = _generator.Dialect;
        var visitor = new PredicateVisitor<TEntity>(_mapping, dialect);
        var (sql, parameters) = visitor.Translate(predicate, ignoreCase);

        var finalSql = $"{_generator.SelectAllSql} WHERE {sql}";
        return _context.QueryAsync<TEntity>(finalSql, parameters);
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

    #endregion

    #region Insert / Update / Delete

    /// <summary>
    /// Inserts a new entity and returns affected row count.
    /// </summary>
    /// <param name="entity">Entity to insert.</param>
    public Task<int> InsertAsync(TEntity entity)
    {
        EnsureCanMutate();
        EntityValidator<TEntity>.ValidateForInsert(entity, _mapping);
        if (string.IsNullOrWhiteSpace(_generator.InsertSql))
        {
            throw new DapperConfigurationException(
                typeof(TEntity).Name,
                "Insert SQL is not configured.");
        }

        return _context.ExecuteAsync(_generator.InsertSql, entity);
    }

    /// <summary>
    /// Updates an existing entity and throws if no rows are affected.
    /// </summary>
    /// <param name="entity">Entity to update.</param>
    public async Task<int> UpdateAsync(TEntity entity)
    {
        EnsureCanMutate();
        EntityValidator<TEntity>.ValidateForUpdate(entity, _mapping);
        if (string.IsNullOrWhiteSpace(_generator.UpdateSql))
        {
            throw new DapperConfigurationException(
                typeof(TEntity).Name,
                "Update SQL is not configured.");
        }

        var affected = await _context.ExecuteAsync(_generator.UpdateSql, entity);
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
    public async Task<int> DeleteAsync(TEntity entity)
    {
        EnsureCanMutate();
        if (string.IsNullOrWhiteSpace(_generator.DeleteByIdSql))
        {
            throw new DapperConfigurationException(
                typeof(TEntity).Name,
                "Delete SQL is not configured.");
        }

        var affected = await _context.ExecuteAsync(_generator.DeleteByIdSql, entity);
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
    public async Task<int> DeleteByIdAsync(object key)
    {
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

        var affected = await _context.ExecuteAsync(_generator.DeleteByIdSql, param);
        if (affected == 0)
        {
            throw new DapperConcurrencyException(OperationType.Delete, typeof(TEntity).Name);
        }

        return affected;
    }

    /// <summary>
    /// Inserts a new entity and returns the generated key value.
    /// </summary>
    /// <typeparam name="TKey">Key value type.</typeparam>
    /// <param name="entity">Entity to insert.</param>
    public async Task<TKey> InsertAndGetIdAsync<TKey>(TEntity entity)
    {
        EnsureCanMutate();
        EntityValidator<TEntity>.ValidateForInsert(entity, _mapping);

        if (_generator.InsertReturningIdSql is null)
        {
            throw new NotSupportedException(
                $"Dialect '{_generator.DialectName}' does not support InsertAndGetIdAsync. " +
                "Use InsertAsync and set the key manually or extend the dialect.");
        }

        TKey? id;
        if (string.Equals(_generator.DialectName, "Oracle", StringComparison.OrdinalIgnoreCase))
        {
            id = await ExecuteOracleInsertReturningAsync<TKey>(entity);
        }
        else
        {
            id = await _context.QueryFirstOrDefaultAsync<TKey>(_generator.InsertReturningIdSql, entity);
        }

        if (id == null || EqualityComparer<TKey>.Default.Equals(id, default!))
        {
            throw new InvalidOperationException(
                $"Identity value returned NULL or default for entity '{typeof(TEntity).Name}'. " +
                "This likely means: (1) insert failed, (2) the provider cannot return identity, " +
                "(3) a trigger suppressed the identity value, or (4) SqlDialect needs adjustments.");
        }

        var keyProp = _generator.KeyProperty;
        try
        {
            if (keyProp is not null)
            {
                var targetType = keyProp.PropertyType;

                object? converted = id;
                if (!targetType.IsAssignableFrom(typeof(TKey)))
                {
                    converted = Convert.ChangeType(id, targetType);
                }

                keyProp.SetValue(entity, converted);
            }
        }
        catch
        {
        }

        return id;
    }

    #endregion

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
    private async Task<TKey> ExecuteOracleInsertReturningAsync<TKey>(TEntity entity)
    {
        var keyProp = _generator.KeyProperty
            ?? throw new InvalidOperationException($"Entity '{typeof(TEntity).Name}' has no key property.");

        var parameters = new DynamicParameters(entity);
        parameters.Add(keyProp.Name, dbType: DbType.Object, direction: ParameterDirection.Output);

        await _context.ExecuteAsync(_generator.InsertReturningIdSql!, parameters);

        return parameters.Get<TKey>(keyProp.Name);
    }
}
