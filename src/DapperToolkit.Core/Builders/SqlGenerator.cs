using System.Reflection;

using DapperToolkit.Core.Interfaces;
using DapperToolkit.Core.Mapping;

namespace DapperToolkit.Core.Builders;

internal sealed class SqlGenerator<TEntity> where TEntity : class
{
    private readonly ISqlDialect _dialect;
    private readonly EntityMapping _mapping;

    public string TableName => _mapping.TableName;
    public string KeyPropertyName => _mapping.KeyProperty.Name;

    public PropertyInfo KeyProperty => _mapping.KeyProperty;
    public bool IsKeyIdentity => _mapping.PropertyMappings.First(pm => pm.Property == _mapping.KeyProperty).IsIdentity;
    public string DialectName => _dialect.Name;

    public string SelectAllSql { get; }
    public string SelectByIdSql { get; }
    public string InsertSql { get; }
    public string? InsertReturningIdSql { get; }
    public string UpdateSql { get; }
    public string DeleteByIdSql { get; }

    public SqlGenerator(ISqlDialect? dialect)
    {
        _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        _mapping = EntityMappingCache<TEntity>.Mapping;

        var fullTableName = BuildFullTableName();
        var keyColumn = GetKeyColumnName();

        SelectAllSql = BuildSelectAllSql(fullTableName);
        SelectByIdSql = BuildSelectByIdSql(SelectAllSql, keyColumn);
        InsertSql = BuildInsertSql(fullTableName);
        InsertReturningIdSql = BuildInsertReturningIdSql(fullTableName, keyColumn);
        UpdateSql = BuildUpdateSql(fullTableName, keyColumn);
        DeleteByIdSql = BuildDeleteSql(fullTableName, keyColumn);
    }

    private string BuildFullTableName()
    {
        var table = _dialect.QuoteIdentifier(_mapping.TableName);

        if (string.IsNullOrWhiteSpace(_mapping.Schema))
            return table;

        return $"{_dialect.QuoteIdentifier(_mapping.Schema!)}.{table}";
    }

    private string GetKeyColumnName()
    {
        var keyMapping = _mapping.PropertyMappings.FirstOrDefault(pm => pm.Property == _mapping.KeyProperty)
            ?? throw new InvalidOperationException($"Key property '{_mapping.KeyProperty.Name}' has no column mapping.");
        return keyMapping.ColumnName;
    }

    private string BuildSelectAllSql(string fullTableName)
    {
        var columnList = string.Join(", ", _mapping.PropertyMappings.Select(pm => $"{_dialect.QuoteIdentifier(pm.ColumnName)} AS {_dialect.QuoteIdentifier(pm.Property.Name)}"));
        return $"SELECT {columnList} FROM {fullTableName}";
    }

    private string BuildSelectByIdSql(string selectAllSql, string keyColumn)
    {
        var param = _dialect.FormatParameter(KeyPropertyName);
        return $"{selectAllSql} WHERE {_dialect.QuoteIdentifier(keyColumn)} = {param}";
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

        var columns = string.Join(", ", insertable.Select(pm => _dialect.QuoteIdentifier(pm.ColumnName)));
        var parameters = string.Join(", ", insertable.Select(pm => _dialect.FormatParameter(pm.Property.Name)));

        return $"INSERT INTO {fullTableName} ({columns}) VALUES ({parameters})";
    }

    private string? BuildInsertReturningIdSql(string fullTableName, string keyColumn)
    {
        try
        {
            return _dialect.BuildInsertReturningId(InsertSql, fullTableName, keyColumn);
        }
        catch (NotSupportedException)
        {
            // optional: or leave empty and let callers ignore it
            return null;
        }
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
            updatable.Select(pm => $"{_dialect.QuoteIdentifier(pm.ColumnName)} = {_dialect.FormatParameter(pm.Property.Name)}")
        );
        var keyParam = _dialect.FormatParameter(KeyPropertyName);
        return $"UPDATE {fullTableName} SET {setClause} WHERE {_dialect.QuoteIdentifier(keyColumn)} = {keyParam}";
    }

    private string BuildDeleteSql(string fullTableName, string keyColumn)
    {
        var keyParam = _dialect.FormatParameter(KeyPropertyName);
        return $"DELETE FROM {fullTableName} WHERE {_dialect.QuoteIdentifier(keyColumn)} = {keyParam}";
    }
}