using Dapper;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;

namespace Nahmadov.DapperForge.Core.Mutations.Bulk;

/// <summary>
/// Builds SQL statements and parameters for bulk operations.
/// </summary>
internal sealed class BulkSqlBuilder<TEntity> where TEntity : class
{
    private readonly IBulkSqlDialect _dialect;
    private readonly EntityMapping _mapping;
    private readonly string _fullTableName;
    private readonly PropertyMapping[] _insertableProperties;
    private readonly PropertyMapping[] _updatableProperties;

    public BulkSqlBuilder(IBulkSqlDialect dialect, EntityMapping mapping)
    {
        _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
        _fullTableName = BuildFullTableName();
        _insertableProperties = GetInsertableProperties();
        _updatableProperties = GetUpdatableProperties();
    }

    /// <summary>
    /// Gets the insertable properties for this entity.
    /// </summary>
    public PropertyMapping[] InsertableProperties => _insertableProperties;

    /// <summary>
    /// Calculates the optimal batch size based on parameter limits.
    /// </summary>
    /// <param name="requestedBatchSize">User-requested batch size (0 for auto).</param>
    /// <returns>Optimal batch size that fits within parameter limits.</returns>
    public int CalculateOptimalBatchSize(int requestedBatchSize)
    {
        var columnsPerRow = _insertableProperties.Length;
        if (columnsPerRow == 0) return 0;

        var maxRows = _dialect.MaxParametersPerStatement / columnsPerRow;

        if (requestedBatchSize <= 0)
            return Math.Min(maxRows, 500); // Default max 500 rows

        return Math.Min(requestedBatchSize, maxRows);
    }

    /// <summary>
    /// Builds a bulk INSERT SQL statement and parameters for the given entities.
    /// </summary>
    public (string sql, DynamicParameters parameters) BuildBulkInsertSql(IReadOnlyList<TEntity> entities)
    {
        var columns = _insertableProperties.Select(p => p.ColumnName).ToList();

        var sql = _dialect.BuildBulkInsert(
            _fullTableName,
            columns,
            entities.Count,
            (rowIndex, columnName) => GetParameterName(rowIndex, columnName));

        var parameters = BuildFlattenedParameters(entities, _insertableProperties);
        return (sql, parameters);
    }

    /// <summary>
    /// Builds a MERGE SQL statement and parameters for the given entities.
    /// </summary>
    public (string sql, DynamicParameters parameters) BuildMergeSql(
        IReadOnlyList<TEntity> entities,
        BulkMergeOptions options)
    {
        var keyColumns = GetKeyColumns(options.MatchColumns);
        var insertColumns = _insertableProperties.Select(p => p.ColumnName).ToList();
        var updateColumns = GetUpdateColumns(options.UpdateColumns, keyColumns);

        var sql = _dialect.BuildMerge(
            _fullTableName,
            keyColumns,
            insertColumns,
            updateColumns,
            entities.Count,
            (rowIndex, columnName) => GetParameterName(rowIndex, columnName),
            options.Mode);

        var parameters = BuildFlattenedParameters(entities, _insertableProperties);
        return (sql, parameters);
    }

    /// <summary>
    /// Builds flattened parameters from entities with indexed names.
    /// </summary>
    private DynamicParameters BuildFlattenedParameters(
        IReadOnlyList<TEntity> entities,
        PropertyMapping[] properties)
    {
        var parameters = new DynamicParameters();
        for (int i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            foreach (var prop in properties)
            {
                var value = prop.Property.GetValue(entity);
                var paramName = GetParameterName(i, prop.ColumnName);
                parameters.Add(paramName, value);
            }
        }
        return parameters;
    }

    /// <summary>
    /// Gets the key columns to use for matching in MERGE operations.
    /// </summary>
    private IReadOnlyList<string> GetKeyColumns(IReadOnlyList<string>? customMatchColumns)
    {
        if (customMatchColumns is { Count: > 0 })
        {
            return customMatchColumns;
        }

        // Use effective key (primary or alternate)
        return _mapping.EffectiveKey
            .Select(p => GetColumnNameForProperty(p))
            .ToList();
    }

    /// <summary>
    /// Gets the columns to update in MERGE operations.
    /// </summary>
    private IReadOnlyList<string> GetUpdateColumns(
        IReadOnlyList<string>? customUpdateColumns,
        IReadOnlyList<string> keyColumns)
    {
        if (customUpdateColumns is { Count: > 0 })
        {
            return customUpdateColumns;
        }

        // Update all updatable columns except key columns
        return _updatableProperties
            .Select(p => p.ColumnName)
            .Where(c => !keyColumns.Contains(c, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private string GetParameterName(int rowIndex, string columnName)
    {
        // Find property name for the column
        var prop = _insertableProperties.FirstOrDefault(p =>
            p.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase));

        var baseName = prop?.Property.Name ?? columnName;
        return $"{baseName}{rowIndex}";
    }

    private string GetColumnNameForProperty(System.Reflection.PropertyInfo property)
    {
        var map = _mapping.PropertyMappings.FirstOrDefault(pm => pm.Property == property)
            ?? throw new InvalidOperationException($"No mapping found for property '{property.Name}'.");
        return map.ColumnName;
    }

    private string BuildFullTableName()
    {
        var table = _dialect.QuoteIdentifier(_mapping.TableName);

        if (string.IsNullOrWhiteSpace(_mapping.Schema))
            return table;

        return $"{_dialect.QuoteIdentifier(_mapping.Schema!)}.{table}";
    }

    private PropertyMapping[] GetInsertableProperties()
    {
        return [.. _mapping.PropertyMappings
            .Where(pm => !pm.IsReadOnly && (!pm.IsGenerated || pm.UsesSequence))];
    }

    private PropertyMapping[] GetUpdatableProperties()
    {
        return [.. _mapping.PropertyMappings
            .Where(pm => !_mapping.EffectiveKey.Contains(pm.Property) && !pm.IsGenerated && !pm.IsReadOnly)];
    }
}
