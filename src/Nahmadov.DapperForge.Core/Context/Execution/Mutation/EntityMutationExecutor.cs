using System.Data;
using System.Reflection;

using Dapper;

using Nahmadov.DapperForge.Core.Builders;
using Nahmadov.DapperForge.Core.Context.Utilities;
using Nahmadov.DapperForge.Core.Exceptions;
using Nahmadov.DapperForge.Core.Mapping;
using Nahmadov.DapperForge.Core.Validation;

namespace Nahmadov.DapperForge.Core.Context.Execution.Mutation;

/// <summary>
/// Encapsulates insert/update/delete operations for a <see cref="DapperSet{TEntity}"/>.
/// </summary>
/// <typeparam name="TEntity">Entity type.</typeparam>
internal sealed class EntityMutationExecutor<TEntity>(DapperDbContext context, SqlGenerator<TEntity> generator, EntityMapping mapping) where TEntity : class
{
    private readonly DapperDbContext _context = context;
    private readonly SqlGenerator<TEntity> _generator = generator;
    private readonly EntityMapping _mapping = mapping;

    public async Task<int> InsertAsync(TEntity entity, IDbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureCanInsert();
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
    /// Provides control over affected row count and supports mass operations.
    /// </summary>
    /// <param name="entity">Entity with updated values.</param>
    /// <param name="where">WHERE conditions as property-value pairs. All columns must exist in entity mapping.</param>
    /// <param name="allowMultiple">Set to true to allow multiple rows to be affected. Default is false (expects exactly 1 row).</param>
    /// <param name="expectedRows">Expected number of affected rows. If specified, throws if actual count differs.</param>
    /// <param name="transaction">Optional transaction.</param>
    /// <returns>Number of affected rows.</returns>
    /// <exception cref="DapperOperationException">If affected row count doesn't match expectations.</exception>
    /// <exception cref="DapperConfigurationException">If WHERE column not found in entity mapping.</exception>
    /// <remarks>
    /// <para><b>Safety:</b></para>
    /// <list type="bullet">
    /// <item>All WHERE columns are validated against entity mapping (prevents SQL injection)</item>
    /// <item>WHERE conditions are parameterized</item>
    /// <item>Patterns like WHERE 1=1 are explicitly forbidden</item>
    /// <item>By default, expects exactly 1 row affected (prevents accidental mass updates)</item>
    /// </list>
    /// </remarks>
    public async Task<int> UpdateAsync(TEntity entity, object where, bool allowMultiple = false, int? expectedRows = null, IDbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(where);
        EnsureCanMutate();
        EntityValidator<TEntity>.ValidateForUpdate(entity, _mapping);

        var whereDict = EntityMutationExecutor<TEntity>.ConvertToDictionary(where);

        var (whereClause, whereParams) = WhereConditionBuilder.BuildFromExplicitConditions(
            whereDict,
            _mapping,
            _generator.Dialect,
            typeof(TEntity).Name);

        WhereConditionBuilder.ValidateSafety(whereClause, typeof(TEntity).Name);

        if (expectedRows.HasValue)
        {
            return await UpdateWithPreValidationAsync(
                entity,
                whereClause,
                whereParams,
                expectedRows.Value,
                transaction).ConfigureAwait(false);
        }

        var updateSql = BuildUpdateSqlWithWhere(whereClause);
        var allParams = EntityMutationExecutor<TEntity>.MergeParameters(entity, whereParams);

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

        // Validate affected row count
        ValidateAffectedRows(affected, allowMultiple, expectedRows, OperationType.Update);

        return affected;
    }

    /// <summary>
    /// Deletes entities using explicit WHERE conditions instead of primary/alternate key.
    /// Provides control over affected row count and supports mass operations.
    /// </summary>
    /// <param name="where">WHERE conditions as property-value pairs. All columns must exist in entity mapping.</param>
    /// <param name="allowMultiple">Set to true to allow multiple rows to be deleted. Default is false (expects exactly 1 row).</param>
    /// <param name="expectedRows">Expected number of affected rows. If specified, throws if actual count differs.</param>
    /// <param name="transaction">Optional transaction.</param>
    /// <returns>Number of affected rows.</returns>
    /// <exception cref="DapperOperationException">If affected row count doesn't match expectations.</exception>
    /// <exception cref="DapperConfigurationException">If WHERE column not found in entity mapping.</exception>
    /// <remarks>
    /// <para><b>Safety:</b></para>
    /// <list type="bullet">
    /// <item>All WHERE columns are validated against entity mapping (prevents SQL injection)</item>
    /// <item>WHERE conditions are parameterized</item>
    /// <item>Patterns like WHERE 1=1 are explicitly forbidden</item>
    /// <item>By default, expects exactly 1 row affected (prevents accidental mass deletes)</item>
    /// </list>
    /// </remarks>
    public async Task<int> DeleteAsync(object where, bool allowMultiple = false, int? expectedRows = null, IDbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(where);
        EnsureCanMutate();

        var whereDict = EntityMutationExecutor<TEntity>.ConvertToDictionary(where);

        var (whereClause, whereParams) = WhereConditionBuilder.BuildFromExplicitConditions(
            whereDict,
            _mapping,
            _generator.Dialect,
            typeof(TEntity).Name);

        WhereConditionBuilder.ValidateSafety(whereClause, typeof(TEntity).Name);

        if (expectedRows.HasValue)
        {
            return await DeleteWithPreValidationAsync(
                whereClause,
                whereParams,
                expectedRows.Value,
                transaction).ConfigureAwait(false);
        }

        var deleteSql = BuildDeleteSql(whereClause);

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

        ValidateAffectedRows(affected, allowMultiple, expectedRows, OperationType.Delete);

        return affected;
    }

    public async Task<TKey> InsertAndGetIdAsync<TKey>(TEntity entity, IDbTransaction? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureCanInsert();
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

    private static Dictionary<string, object?> ConvertToDictionary(object obj)
    {
        var dict = new Dictionary<string, object?>();
        var properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            var value = prop.GetValue(obj);
            dict[prop.Name] = value;
        }

        return dict;
    }

    private string BuildUpdateSqlWithWhere(string whereClause)
    {
        if (string.IsNullOrWhiteSpace(_generator.UpdateSql))
        {
            throw new DapperConfigurationException(
                typeof(TEntity).Name,
                "Update SQL is not configured or no columns are updatable.");
        }

        var sqlBeforeWhere = _generator.UpdateSql.Substring(0, _generator.UpdateSql.LastIndexOf(" WHERE ", StringComparison.OrdinalIgnoreCase));
        return $"{sqlBeforeWhere} WHERE {whereClause}";
    }

    private static object MergeParameters(TEntity entity, Dictionary<string, object?> whereParams)
    {
        var parameters = new DynamicParameters(entity);

        foreach (var (key, value) in whereParams)
        {
            parameters.Add(key, value);
        }

        return parameters;
    }

    private string BuildDeleteSql(string whereClause)
    {
        var tableName = string.IsNullOrWhiteSpace(_mapping.Schema)
            ? _generator.Dialect.QuoteIdentifier(_mapping.TableName)
            : $"{_generator.Dialect.QuoteIdentifier(_mapping.Schema)}.{_generator.Dialect.QuoteIdentifier(_mapping.TableName)}";

        return $"DELETE FROM {tableName} WHERE {whereClause}";
    }

    private async Task<int> UpdateWithPreValidationAsync(
        TEntity entity,
        string whereClause,
        Dictionary<string, object?> whereParams,
        int expectedRows,
        IDbTransaction? transaction)
    {
        // If transaction provided by caller, use it directly
        if (transaction is not null)
        {
            return await ExecuteUpdateWithPreValidationAsync(
                entity, whereClause, whereParams, expectedRows, transaction)
                .ConfigureAwait(false);
        }

        // Otherwise, create transaction scope for automatic rollback on failure
        using var txScope = await _context.BeginTransactionScopeAsync().ConfigureAwait(false);

        try
        {
            var affected = await ExecuteUpdateWithPreValidationAsync(
                entity, whereClause, whereParams, expectedRows, txScope.Transaction)
                .ConfigureAwait(false);

            // Mark transaction as successful
            txScope.Complete();

            return affected;
        }
        catch
        {
            // Transaction will be automatically rolled back on dispose
            // Even if rollback fails, TransactionScope handles it gracefully
            throw;
        }
    }

    /// <summary>
    /// Executes the actual update with pre-validation logic.
    /// </summary>
    private async Task<int> ExecuteUpdateWithPreValidationAsync(
        TEntity entity,
        string whereClause,
        Dictionary<string, object?> whereParams,
        int expectedRows,
        IDbTransaction transaction)
    {
        var countSql = BuildCountSql(whereClause);
        long actualCount;

        try
        {
            actualCount = await _context.QueryFirstOrDefaultAsync<long>(
                countSql, whereParams, transaction).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not DapperForgeException)
        {
            throw new DapperExecutionException(OperationType.Update, typeof(TEntity).Name, countSql, ex);
        }

        // Validate count BEFORE executing update
        if (actualCount != expectedRows)
        {
            throw new DapperOperationException(
                OperationType.Update,
                typeof(TEntity).Name,
                $"Expected {expectedRows} row(s) to be affected but found {actualCount} matching row(s) before update.");
        }

        // Execute UPDATE
        var updateSql = BuildUpdateSqlWithWhere(whereClause);
        var allParams = EntityMutationExecutor<TEntity>.MergeParameters(entity, whereParams);

        int affected;
        try
        {
            affected = await _context.ExecuteAsync(updateSql, allParams, transaction).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not DapperForgeException)
        {
            throw new DapperExecutionException(OperationType.Update, typeof(TEntity).Name, updateSql, ex);
        }

        return affected;
    }

    private async Task<int> DeleteWithPreValidationAsync(
        string whereClause,
        Dictionary<string, object?> whereParams,
        int expectedRows,
        IDbTransaction? transaction)
    {
        // If transaction provided by caller, use it directly
        if (transaction is not null)
        {
            return await ExecuteDeleteWithPreValidationAsync(
                whereClause, whereParams, expectedRows, transaction)
                .ConfigureAwait(false);
        }

        // Otherwise, create transaction scope for automatic rollback on failure
        using var txScope = await _context.BeginTransactionScopeAsync().ConfigureAwait(false);

        try
        {
            var affected = await ExecuteDeleteWithPreValidationAsync(
                whereClause, whereParams, expectedRows, txScope.Transaction)
                .ConfigureAwait(false);

            // Mark transaction as successful
            txScope.Complete();

            return affected;
        }
        catch
        {
            // Transaction will be automatically rolled back on dispose
            // Even if rollback fails, TransactionScope handles it gracefully
            throw;
        }
    }

    /// <summary>
    /// Executes the actual delete with pre-validation logic.
    /// </summary>
    private async Task<int> ExecuteDeleteWithPreValidationAsync(
        string whereClause,
        Dictionary<string, object?> whereParams,
        int expectedRows,
        IDbTransaction transaction)
    {
        var countSql = BuildCountSql(whereClause);
        long actualCount;

        try
        {
            actualCount = await _context.QueryFirstOrDefaultAsync<long>(
                countSql, whereParams, transaction).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not DapperForgeException)
        {
            throw new DapperExecutionException(
                OperationType.Delete,
                typeof(TEntity).Name,
                countSql,
                ex);
        }

        if (actualCount != expectedRows)
        {
            throw new DapperOperationException(
                OperationType.Delete,
                typeof(TEntity).Name,
                $"Expected {expectedRows} row(s) to be affected but found {actualCount} matching row(s) before delete.");
        }

        var deleteSql = BuildDeleteSql(whereClause);

        int affected;
        try
        {
            affected = await _context.ExecuteAsync(deleteSql, whereParams, transaction).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not DapperForgeException)
        {
            throw new DapperExecutionException(OperationType.Delete, typeof(TEntity).Name, deleteSql, ex);
        }

        return affected;
    }

    private string BuildCountSql(string whereClause)
    {
        var tableName = string.IsNullOrWhiteSpace(_mapping.Schema)
            ? _generator.Dialect.QuoteIdentifier(_mapping.TableName)
            : $"{_generator.Dialect.QuoteIdentifier(_mapping.Schema)}.{_generator.Dialect.QuoteIdentifier(_mapping.TableName)}";

        return $"SELECT COUNT(*) FROM {tableName} WHERE {whereClause}";
    }

    private static void ValidateAffectedRows(int affected, bool allowMultiple, int? expectedRows, OperationType operationType)
    {
        if (expectedRows.HasValue)
        {
            if (affected != expectedRows.Value)
            {
                throw new DapperOperationException(operationType, typeof(TEntity).Name, $"Expected {expectedRows.Value} row(s) to be affected but {affected} row(s) were affected.");
            }
            return;
        }

        if (!allowMultiple && affected != 1)
        {
            if (affected == 0)
            {
                throw new DapperConcurrencyException(operationType, typeof(TEntity).Name);
            }
            else
            {
                throw new DapperOperationException(operationType, typeof(TEntity).Name, $"Expected 1 row to be affected but {affected} rows were affected. " + "Set allowMultiple=true to allow multiple rows to be affected.");
            }
        }
    }

    private void EnsureCanInsert()
    {
        if (_mapping.IsReadOnly)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' is marked as ReadOnly and cannot be modified.");
        }
    }

    private void EnsureCanMutate()
    {
        if (_mapping.IsReadOnly)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' is marked as ReadOnly and cannot be modified.");
        }
        if (!_mapping.HasPrimaryKey && !_mapping.HasAlternateKey)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' has no Primary Key or Alternate Key and cannot be updated/deleted.");
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
