using DapperToolkit.Core.Mapping;

namespace DapperToolkit.Core.Builders;

internal sealed class SqlGenerator<TEntity> where TEntity : class
{
    private readonly EntityMapping _mapping;

    public string TableName => _mapping.TableName;
    public string KeyPropertyName => _mapping.KeyProperty.Name;

    public string SelectAllSql { get; }
    public string SelectByIdSql { get; }
    public string InsertSql { get; }
    public string UpdateSql { get; }
    public string DeleteByIdSql { get; }

    public SqlGenerator()
    {
        _mapping = EntityMappingCache<TEntity>.Mapping;

        var fullTableName = BuildFullTableName();
        var keyColumn = GetKeyColumnName();

        SelectAllSql = BuildSelectAllSql(fullTableName);
        SelectByIdSql = BuildSelectByIdSql(SelectAllSql, keyColumn);
        InsertSql = BuildInsertSql(fullTableName);
        UpdateSql = BuildUpdateSql(fullTableName, keyColumn);
        DeleteByIdSql = BuildDeleteSql(fullTableName, keyColumn);
    }

    private string BuildFullTableName()
    {
        if (string.IsNullOrWhiteSpace(_mapping.Schema))
            return _mapping.TableName;

        return $"{_mapping.Schema}.{_mapping.TableName}";
    }

    private string GetKeyColumnName()
    {
        var keyMapping = _mapping.PropertyMappings.FirstOrDefault(pm => pm.Property == _mapping.KeyProperty)
            ?? throw new InvalidOperationException($"Key property '{_mapping.KeyProperty.Name}' has no column mapping.");
        return keyMapping.ColumnName;
    }

    private string BuildSelectAllSql(string fullTableName)
    {
        var columnList = string.Join(", ", _mapping.PropertyMappings.Select(pm => $"{pm.ColumnName} AS {pm.Property.Name}"));
        return $"SELECT {columnList} FROM {fullTableName}";
    }

    private string BuildSelectByIdSql(string selectAllSql, string keyColumn)
    {
        return $"{selectAllSql} WHERE {keyColumn} = @{KeyPropertyName}";
    }

    private string BuildInsertSql(string fullTableName)
    {
        var insertable = _mapping.PropertyMappings
            .Where(pm => !pm.IsGenerated)    // Identity + Computed çıxarıldı
            .ToList();

        if (insertable.Count == 0)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' has no insertable columns. " +
                "All properties are marked as DatabaseGenerated or NotMapped."
            );
        }

        var columns = string.Join(", ", insertable.Select(pm => pm.ColumnName));
        var parameters = string.Join(", ", insertable.Select(pm => "@" + pm.Property.Name));

        return $"INSERT INTO {fullTableName} ({columns}) VALUES ({parameters})";
    }

    private string BuildUpdateSql(string fullTableName, string keyColumn)
    {
        var updatable = _mapping.PropertyMappings
            .Where(pm => pm.Property != _mapping.KeyProperty && !pm.IsGenerated)
            .ToList();

        if (updatable.Count == 0)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' has no updatable columns. " +
                "All properties are marked as DatabaseGenerated, Computed, or NotMapped."
            );
        }

        var setClause = string.Join(", ",
            updatable.Select(pm => $"{pm.ColumnName} = @{pm.Property.Name}")
        );

        return $"UPDATE {fullTableName} SET {setClause} WHERE {keyColumn} = @{KeyPropertyName}";
    }

    private string BuildDeleteSql(string fullTableName, string keyColumn)
    {
        return $"DELETE FROM {fullTableName} WHERE {keyColumn} = @{KeyPropertyName}";
    }
}