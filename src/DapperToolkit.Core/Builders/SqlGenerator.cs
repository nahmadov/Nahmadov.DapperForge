using System.Reflection;

using DapperToolkit.Core.Interfaces;
using DapperToolkit.Core.Mapping;

namespace DapperToolkit.Core.Builders;

internal sealed class SqlGenerator<TEntity> where TEntity : class
{
    private readonly ISqlDialect _dialect;
    private readonly EntityMapping _mapping;

    public string TableName => _mapping.TableName;
    public string? KeyPropertyName => _mapping.KeyProperty?.Name;

    public PropertyInfo? KeyProperty => _mapping.KeyProperty;
    public bool IsKeyIdentity =>
        _mapping.KeyProperty is null
            ? false
            : _mapping.PropertyMappings
                .FirstOrDefault(pm => pm.Property == _mapping.KeyProperty)?.IsIdentity ?? false;

    public string DialectName => _dialect.Name;

    public string SelectAllSql { get; }
    public string SelectByIdSql { get; }
    public string InsertSql { get; }
    public string? InsertReturningIdSql { get; }
    public string UpdateSql { get; }
    public string DeleteByIdSql { get; }

    public ISqlDialect Dialect => _dialect;

    public SqlGenerator(ISqlDialect? dialect)
    {
        _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        _mapping = EntityMappingCache<TEntity>.Mapping;

        var fullTableName = BuildFullTableName();
        var keyColumn     = GetKeyColumnName();
        var hasKey        = _mapping.KeyProperty is not null;

        // 1) Hər zaman SELECT *
        SelectAllSql = BuildSelectAllSql(fullTableName);

        // 2) ById yalnız key olduqda
        if (hasKey && keyColumn is not null && KeyPropertyName is not null)
        {
            SelectByIdSql = BuildSelectByIdSql(SelectAllSql, keyColumn);
        }
        else
        {
            SelectByIdSql = string.Empty;
        }

        // 3) Mutating SQL yalnız non-readonly + key varsa
        if (!_mapping.IsReadOnly && hasKey && keyColumn is not null && KeyPropertyName is not null)
        {
            InsertSql           = BuildInsertSql(fullTableName);
            InsertReturningIdSql = BuildInsertReturningIdSql(fullTableName, keyColumn);
            UpdateSql           = BuildUpdateSql(fullTableName, keyColumn);
            DeleteByIdSql       = BuildDeleteSql(fullTableName, keyColumn);
        }
        else
        {
            InsertSql           = string.Empty;
            InsertReturningIdSql = null;
            UpdateSql           = string.Empty;
            DeleteByIdSql       = string.Empty;
        }
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

    private string BuildSelectAllSql(string fullTableName)
    {
        var columnList = string.Join(
            ", ",
            _mapping.PropertyMappings.Select(pm =>
                $"{_dialect.QuoteIdentifier(pm.ColumnName)} AS {_dialect.QuoteIdentifier(pm.Property.Name)}"));

        return $"SELECT {columnList} FROM {fullTableName}";
    }

    private string BuildSelectByIdSql(string selectAllSql, string keyColumn)
    {
        if (KeyPropertyName is null)
            throw new InvalidOperationException("Cannot build SelectById SQL without a key property.");

        var param = _dialect.FormatParameter(KeyPropertyName);
        return $"{selectAllSql} WHERE {_dialect.QuoteIdentifier(keyColumn)} = {param}";
    }

    private string BuildInsertSql(string fullTableName)
    {
        if (_mapping.IsReadOnly)
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' is marked as ReadOnly and cannot be modified.");

        var insertable = _mapping.PropertyMappings
            .Where(pm => !pm.IsGenerated)
            .ToList();

        if (insertable.Count == 0)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' has no insertable columns. " +
                "All properties are marked as DatabaseGenerated or NotMapped.");
        }

        var columns = string.Join(", ", insertable.Select(pm => _dialect.QuoteIdentifier(pm.ColumnName)));
        var parameters = string.Join(", ", insertable.Select(pm => _dialect.FormatParameter(pm.Property.Name)));

        return $"INSERT INTO {fullTableName} ({columns}) VALUES ({parameters})";
    }

    private string? BuildInsertReturningIdSql(string fullTableName, string keyColumn)
    {
        // Buraya yalnız non-readonly + key varsa gəlirik
        try
        {
            return _dialect.BuildInsertReturningId(InsertSql, fullTableName, keyColumn);
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private string BuildUpdateSql(string fullTableName, string keyColumn)
    {
        if (_mapping.IsReadOnly || KeyPropertyName is null)
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' is marked as ReadOnly and cannot be modified.");

        var updatable = _mapping.PropertyMappings
            .Where(pm => pm.Property != _mapping.KeyProperty && !pm.IsGenerated)
            .ToList();

        if (updatable.Count == 0)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' has no updatable columns. " +
                "All properties are marked as DatabaseGenerated, Computed, or NotMapped.");
        }

        var setClause = string.Join(
            ", ",
            updatable.Select(pm =>
                $"{_dialect.QuoteIdentifier(pm.ColumnName)} = {_dialect.FormatParameter(pm.Property.Name)}"));

        var keyParam = _dialect.FormatParameter(KeyPropertyName);
        return $"UPDATE {fullTableName} SET {setClause} WHERE {_dialect.QuoteIdentifier(keyColumn)} = {keyParam}";
    }

    private string BuildDeleteSql(string fullTableName, string keyColumn)
    {
        if (_mapping.IsReadOnly || KeyPropertyName is null)
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' is marked as ReadOnly and cannot be modified.");

        var keyParam = _dialect.FormatParameter(KeyPropertyName);
        return $"DELETE FROM {fullTableName} WHERE {_dialect.QuoteIdentifier(keyColumn)} = {keyParam}";
    }
}
