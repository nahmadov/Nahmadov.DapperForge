using System.Data;
using System.Diagnostics;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.Core.Infrastructure.Exceptions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;
using Nahmadov.DapperForge.Core.Mutations.Execution;

namespace Nahmadov.DapperForge.Core.Mutations.Bulk;

/// <summary>
/// Executes bulk insert and merge operations with batching and transaction support.
/// </summary>
internal sealed class BulkMutationExecutor<TEntity> where TEntity : class
{
    private readonly DapperDbContext _context;
    private readonly BulkSqlBuilder<TEntity> _sqlBuilder;
    private readonly EntityMapping _mapping;
    private readonly MutationValidator<TEntity> _validator;
    private readonly string _entityName;

    public BulkMutationExecutor(
        DapperDbContext context,
        IBulkSqlDialect dialect,
        EntityMapping mapping)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        _sqlBuilder = new BulkSqlBuilder<TEntity>(dialect, mapping);
        _validator = new MutationValidator<TEntity>(mapping);
        _entityName = typeof(TEntity).Name;
    }

    /// <summary>
    /// Performs a bulk insert operation on the specified entities.
    /// </summary>
    public async Task<BulkOperationResult> BulkInsertAsync(
        IEnumerable<TEntity> entities,
        BulkInsertOptions? options = null,
        IDbTransaction? transaction = null)
    {
        options ??= new BulkInsertOptions();
        var entityList = entities.ToList();

        if (entityList.Count == 0)
            return new BulkOperationResult { TotalAffected = 0, BatchCount = 0, ElapsedTime = TimeSpan.Zero };

        _validator.EnsureCanInsert();

        if (options.ValidateBeforeInsert)
            ValidateAllEntities(entityList, isInsert: true);

        var batchSize = _sqlBuilder.CalculateOptimalBatchSize(options.BatchSize);
        if (batchSize == 0)
        {
            throw new DapperConfigurationException(
                _entityName,
                "Entity has no insertable columns.");
        }

        var stopwatch = Stopwatch.StartNew();
        var totalAffected = 0;
        var batchCount = 0;

        foreach (var batch in Chunk(entityList, batchSize))
        {
            var batchList = batch.ToList();
            var (sql, parameters) = _sqlBuilder.BuildBulkInsertSql(batchList);

            try
            {
                totalAffected += await _context.ExecuteAsync(sql, parameters, transaction).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not DapperForgeException)
            {
                throw new DapperExecutionException(
                    OperationType.BulkInsert,
                    _entityName,
                    sql,
                    ex);
            }

            batchCount++;
        }

        stopwatch.Stop();
        return new BulkOperationResult
        {
            TotalAffected = totalAffected,
            BatchCount = batchCount,
            ElapsedTime = stopwatch.Elapsed
        };
    }

    /// <summary>
    /// Performs a bulk merge (upsert) operation on the specified entities.
    /// </summary>
    public async Task<BulkMergeResult> BulkMergeAsync(
        IEnumerable<TEntity> entities,
        BulkMergeOptions? options = null,
        IDbTransaction? transaction = null)
    {
        options ??= new BulkMergeOptions();
        var entityList = entities.ToList();

        if (entityList.Count == 0)
            return new BulkMergeResult { TotalAffected = 0, BatchCount = 0, ElapsedTime = TimeSpan.Zero };

        ValidateMergeConfiguration(options);

        if (options.ValidateBeforeMerge)
            ValidateAllEntities(entityList, isInsert: false);

        var batchSize = _sqlBuilder.CalculateOptimalBatchSize(options.BatchSize);
        if (batchSize == 0)
        {
            throw new DapperConfigurationException(
                _entityName,
                "Entity has no insertable columns.");
        }

        var stopwatch = Stopwatch.StartNew();
        var totalAffected = 0;
        var batchCount = 0;

        foreach (var batch in Chunk(entityList, batchSize))
        {
            var batchList = batch.ToList();
            var (sql, parameters) = _sqlBuilder.BuildMergeSql(batchList, options);

            try
            {
                totalAffected += await _context.ExecuteAsync(sql, parameters, transaction).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not DapperForgeException)
            {
                throw new DapperExecutionException(
                    OperationType.BulkMerge,
                    _entityName,
                    sql,
                    ex);
            }

            batchCount++;
        }

        stopwatch.Stop();
        return new BulkMergeResult
        {
            TotalAffected = totalAffected,
            BatchCount = batchCount,
            ElapsedTime = stopwatch.Elapsed
        };
    }

    /// <summary>
    /// Validates all entities before bulk operation.
    /// </summary>
    private void ValidateAllEntities(IReadOnlyList<TEntity> entities, bool isInsert)
    {
        var errors = new Dictionary<int, IReadOnlyList<string>>();

        for (int i = 0; i < entities.Count; i++)
        {
            var entityErrors = ValidateEntity(entities[i], isInsert);
            if (entityErrors.Count > 0)
            {
                errors[i] = entityErrors;
            }
        }

        if (errors.Count > 0)
        {
            throw new DapperBulkValidationException(_entityName, errors);
        }
    }

    /// <summary>
    /// Validates a single entity and returns validation errors.
    /// </summary>
    private List<string> ValidateEntity(TEntity entity, bool isInsert)
    {
        var errors = new List<string>();

        if (entity == null)
        {
            errors.Add("Entity is null.");
            return errors;
        }

        if (_mapping.IsReadOnly)
        {
            errors.Add("Entity is marked as ReadOnly and cannot be modified.");
            return errors;
        }

        foreach (var propMap in _mapping.PropertyMappings)
        {
            if (isInsert && propMap.IsGenerated) continue;
            if (!isInsert && propMap.IsReadOnly) continue;
            if (!isInsert && propMap.IsGenerated) continue;

            var prop = propMap.Property;
            var value = prop.GetValue(entity);
            var displayName = $"'{prop.Name}'";

            if (propMap.IsRequired && value is null)
            {
                errors.Add($"Property {displayName} is required.");
                continue;
            }

            if (value is string str && propMap.MaxLength.HasValue && str.Length > propMap.MaxLength.Value)
            {
                errors.Add($"Property {displayName} exceeds maximum length of {propMap.MaxLength.Value}.");
            }
        }

        return errors;
    }

    /// <summary>
    /// Validates merge configuration.
    /// </summary>
    private void ValidateMergeConfiguration(BulkMergeOptions options)
    {
        _validator.EnsureCanInsert();

        // For merge operations, we need either:
        // 1. Custom match columns specified in options, OR
        // 2. An effective key (primary or alternate) defined on the entity
        if ((options.MatchColumns == null || options.MatchColumns.Count == 0) &&
            !_mapping.HasPrimaryKey && !_mapping.HasAlternateKey)
        {
            throw new DapperConfigurationException(
                _entityName,
                "Merge operation requires either MatchColumns in options or a Primary/Alternate Key on the entity.");
        }

        // Validate that match columns exist in the entity mapping
        if (options.MatchColumns != null && options.MatchColumns.Count > 0)
        {
            var validColumns = _mapping.PropertyMappings.Select(p => p.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var col in options.MatchColumns)
            {
                if (!validColumns.Contains(col))
                {
                    throw new DapperConfigurationException(
                        _entityName,
                        $"Match column '{col}' does not exist in entity mapping.");
                }
            }
        }

        // Validate update columns if specified
        if (options.UpdateColumns != null && options.UpdateColumns.Count > 0)
        {
            var validColumns = _mapping.PropertyMappings.Select(p => p.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var col in options.UpdateColumns)
            {
                if (!validColumns.Contains(col))
                {
                    throw new DapperConfigurationException(
                        _entityName,
                        $"Update column '{col}' does not exist in entity mapping.");
                }
            }
        }
    }

    /// <summary>
    /// Splits a list into chunks of the specified size.
    /// </summary>
    private static IEnumerable<IEnumerable<T>> Chunk<T>(IReadOnlyList<T> source, int chunkSize)
    {
        for (int i = 0; i < source.Count; i += chunkSize)
        {
            yield return source.Skip(i).Take(chunkSize);
        }
    }
}
