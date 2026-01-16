using System.Data;
using System.Reflection;

using Dapper;

using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.Core.Infrastructure.Exceptions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;
using Nahmadov.DapperForge.Core.Modeling.Validation;
using Nahmadov.DapperForge.Core.Mutations.Sql;
using Nahmadov.DapperForge.Core.Querying.Sql;

namespace Nahmadov.DapperForge.Core.Mutations.Execution;
/// <summary>
/// Encapsulates insert/update/delete operations for a <see cref="DapperSet{TEntity}"/>.
/// </summary>
/// <typeparam name="TEntity">Entity type.</typeparam>
internal sealed class EntityMutationExecutor<TEntity> where TEntity : class
{
    private readonly DapperDbContext _context;
    private readonly SqlGenerator<TEntity> _generator;
    private readonly EntityMapping _mapping;
    private readonly MutationValidator<TEntity> _validator;
    private readonly MutationSqlBuilder<TEntity> _sqlBuilder;
    private readonly PreValidationMutationExecutor<TEntity> _preValidationExecutor;

    public EntityMutationExecutor(DapperDbContext context, SqlGenerator<TEntity> generator, EntityMapping mapping)
    {
        _context = context;
        _generator = generator;
        _mapping = mapping;
        _validator = new MutationValidator<TEntity>(mapping);
        _sqlBuilder = new MutationSqlBuilder<TEntity>(generator, mapping);
        _preValidationExecutor = new PreValidationMutationExecutor<TEntity>(context, _sqlBuilder);
    }

    public async Task<int> InsertAsync(TEntity entity, IDbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _validator.EnsureCanInsert();
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

    public async Task<int> UpdateAsync(TEntity entity, IDbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _validator.EnsureCanMutate();
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

    public async Task<int> DeleteAsync(TEntity entity, IDbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _validator.EnsureCanMutate();

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

    public async Task<int> DeleteByIdAsync(object key, IDbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        _validator.EnsureCanMutate();

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

        var param = KeyParameterBuilder.Build(_mapping, key, typeof(TEntity).Name);

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
    /// Updates an entity using explicit WHERE conditions instead of primary/alternate key.
    /// </summary>
    public async Task<int> UpdateAsync(
        TEntity entity,
        object where,
        bool allowMultiple = false,
        int? expectedRows = null,
        IDbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(where);
        _validator.EnsureCanMutate();
        EntityValidator<TEntity>.ValidateForUpdate(entity, _mapping);

        var whereDict = MutationParameterHelper.ConvertToDictionary(where);

        var (whereClause, whereParams) = WhereConditionBuilder.BuildFromExplicitConditions(
            whereDict,
            _mapping,
            _generator.Dialect,
            typeof(TEntity).Name);

        WhereConditionBuilder.ValidateSafety(whereClause, typeof(TEntity).Name);

        if (expectedRows.HasValue)
        {
            return await _preValidationExecutor.UpdateWithPreValidationAsync(
                entity,
                whereClause,
                whereParams,
                expectedRows.Value,
                transaction).ConfigureAwait(false);
        }

        var updateSql = _sqlBuilder.BuildUpdateSqlWithWhere(whereClause);
        var allParams = MutationParameterHelper.MergeParameters(entity, whereParams);

        int affected;
        try
        {
            affected = await _context.ExecuteAsync(updateSql, allParams, transaction).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not DapperForgeException)
        {
            throw new DapperExecutionException(
                OperationType.Update,
                typeof(TEntity).Name,
                updateSql,
                ex);
        }

        MutationValidator<TEntity>.ValidateAffectedRows(affected, allowMultiple, expectedRows, OperationType.Update);

        return affected;
    }

    /// <summary>
    /// Deletes entities using explicit WHERE conditions instead of primary/alternate key.
    /// </summary>
    public async Task<int> DeleteAsync(
        object where,
        bool allowMultiple = false,
        int? expectedRows = null,
        IDbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(where);
        _validator.EnsureCanMutate();

        var whereDict = MutationParameterHelper.ConvertToDictionary(where);

        var (whereClause, whereParams) = WhereConditionBuilder.BuildFromExplicitConditions(
            whereDict,
            _mapping,
            _generator.Dialect,
            typeof(TEntity).Name);

        WhereConditionBuilder.ValidateSafety(whereClause, typeof(TEntity).Name);

        if (expectedRows.HasValue)
        {
            return await _preValidationExecutor.DeleteWithPreValidationAsync(
                whereClause,
                whereParams,
                expectedRows.Value,
                transaction).ConfigureAwait(false);
        }

        var deleteSql = _sqlBuilder.BuildDeleteSql(whereClause);

        int affected;
        try
        {
            affected = await _context.ExecuteAsync(deleteSql, whereParams, transaction).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not DapperForgeException)
        {
            throw new DapperExecutionException(
                OperationType.Delete,
                typeof(TEntity).Name,
                deleteSql,
                ex);
        }

        MutationValidator<TEntity>.ValidateAffectedRows(affected, allowMultiple, expectedRows, OperationType.Delete);

        return affected;
    }

    public async Task<TKey> InsertAndGetIdAsync<TKey>(TEntity entity, IDbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        _validator.EnsureCanInsert();
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
                id = await _context.QueryFirstOrDefaultAsync<TKey>(
                    _generator.InsertReturningIdSql, entity, transaction).ConfigureAwait(false);
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

    private async Task<TKey> ExecuteOracleInsertReturningAsync<TKey>(TEntity entity, IDbTransaction? transaction)
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


