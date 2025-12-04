using System.Reflection;

using DapperToolkit.Core.Interfaces;

using DapperToolkit.Core.Mapping;

namespace DapperToolkit.Core.Builders;

internal sealed class SqlGenerator<TEntity> where TEntity : class
{
    private readonly ISqlDialect _dialect;
    private readonly EntityMapping _mapping;
    private readonly string _fullTableName;
    private readonly string? _keyColumn;
    private readonly PropertyMapping[] _insertableProperties;
    private readonly PropertyMapping[] _updatableProperties;

    public string TableName => _mapping.TableName;
    public string? KeyPropertyName => _mapping.KeyProperty?.Name;
    public PropertyInfo? KeyProperty => _mapping.KeyProperty;

    public bool IsKeyIdentity =>
        _mapping.KeyProperty is not null && (_mapping.PropertyMappings
                .FirstOrDefault(pm => pm.Property == _mapping.KeyProperty)?.IsIdentity ?? false);

    public string DialectName => _dialect.Name;

    public string SelectAllSql { get; }
    public string SelectByIdSql { get; }
    public string InsertSql { get; }
    public string? InsertReturningIdSql { get; }
    public string UpdateSql { get; }
    public string DeleteByIdSql { get; }

    public ISqlDialect Dialect => _dialect;

    public SqlGenerator(ISqlDialect? dialect, EntityMapping mapping)
    {
        _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));

        _fullTableName = BuildFullTableName();
        _keyColumn = GetKeyColumnName();
        _insertableProperties = [.. _mapping.PropertyMappings.Where(pm => !pm.IsGenerated)];
        _updatableProperties = [.. _mapping.PropertyMappings.Where(pm => pm.Property != _mapping.KeyProperty && !pm.IsGenerated)];

        SelectAllSql = BuildSelectAllSql();
        SelectByIdSql = BuildSelectByIdSql();

        (InsertSql, InsertReturningIdSql, UpdateSql, DeleteByIdSql) = BuildMutatingSql();
    }

    private string BuildFullTableName()
    {
        var table = _dialect.QuoteIdentifier(_mapping.TableName);

        if (string.IsNullOrWhiteSpace(_mapping.Schema))
            return table;

        return $"{_dialect.QuoteIdentifier(_mapping.Schema!)}.{table}";
    }

    private string? GetKeyColumnName()
    {
        if (_mapping.KeyProperty is null)
            return null;

        var keyMapping = _mapping.PropertyMappings
            .FirstOrDefault(pm => pm.Property == _mapping.KeyProperty);

        return keyMapping?.ColumnName;
    }

    private string BuildSelectAllSql()
    {
        var columnList = string.Join(
            ", ",
            _mapping.PropertyMappings.Select(pm =>
                $"{_dialect.QuoteIdentifier(pm.ColumnName)} AS {_dialect.QuoteIdentifier(pm.Property.Name)}"));

        return $"SELECT {columnList} FROM {_fullTableName}";
    }

    private string BuildSelectByIdSql()
    {
        if (_keyColumn is null || KeyPropertyName is null)
            return string.Empty;

        var param = _dialect.FormatParameter(KeyPropertyName);
        return $"{SelectAllSql} WHERE {_dialect.QuoteIdentifier(_keyColumn)} = {param}";
    }

    private (string insert, string? insertReturningId, string update, string delete) BuildMutatingSql()
    {
        if (_mapping.IsReadOnly || _keyColumn is null || KeyPropertyName is null)
            return (string.Empty, null, string.Empty, string.Empty);

        var insert = BuildInsertSql();
        var insertReturningId = BuildInsertReturningIdSql(insert);
        var update = BuildUpdateSql();
        var delete = BuildDeleteSql();

        return (insert, insertReturningId, update, delete);
    }

    private string BuildInsertSql()
    {
        if (_mapping.IsReadOnly)
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' is marked as ReadOnly and cannot be modified.");

        if (_insertableProperties.Length == 0)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' has no insertable columns. " +
                "All properties are marked as DatabaseGenerated or NotMapped.");
        }

        var columns = string.Join(", ", _insertableProperties.Select(pm => _dialect.QuoteIdentifier(pm.ColumnName)));
        var parameters = string.Join(", ", _insertableProperties.Select(pm => _dialect.FormatParameter(pm.Property.Name)));

        return $"INSERT INTO {_fullTableName} ({columns}) VALUES ({parameters})";
    }

    private string? BuildInsertReturningIdSql(string insertSql)
    {
        try
        {
            return _dialect.BuildInsertReturningId(insertSql, _fullTableName, _keyColumn!);
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private string BuildUpdateSql()
    {
        if (_mapping.IsReadOnly || KeyPropertyName is null || _keyColumn is null)
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' is marked as ReadOnly and cannot be modified.");

        if (_updatableProperties.Length == 0)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' has no updatable columns. " +
                "All properties are marked as DatabaseGenerated, Computed, or NotMapped.");
        }

        var setClause = string.Join(
            ", ",
            _updatableProperties.Select(pm =>
                $"{_dialect.QuoteIdentifier(pm.ColumnName)} = {_dialect.FormatParameter(pm.Property.Name)}"));

        var keyParam = _dialect.FormatParameter(KeyPropertyName);
        return $"UPDATE {_fullTableName} SET {setClause} WHERE {_dialect.QuoteIdentifier(_keyColumn)} = {keyParam}";
    }

    private string BuildDeleteSql()
    {
        if (_mapping.IsReadOnly || KeyPropertyName is null || _keyColumn is null)
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' is marked as ReadOnly and cannot be modified.");

        var keyParam = _dialect.FormatParameter(KeyPropertyName);
        return $"DELETE FROM {_fullTableName} WHERE {_dialect.QuoteIdentifier(_keyColumn)} = {keyParam}";
    }
}
