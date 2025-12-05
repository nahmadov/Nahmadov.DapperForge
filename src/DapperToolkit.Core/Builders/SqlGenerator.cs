using System.Reflection;

using DapperToolkit.Core.Interfaces;
using DapperToolkit.Core.Mapping;

namespace DapperToolkit.Core.Builders;

internal sealed class SqlGenerator<TEntity> where TEntity : class
{
    private readonly ISqlDialect _dialect;
    private readonly EntityMapping _mapping;
    private readonly string _fullTableName;
    private readonly string[] _keyColumns;
    private readonly PropertyMapping[] _insertableProperties;
    private readonly PropertyMapping[] _updatableProperties;

    public string TableName => _mapping.TableName;
    public string? KeyPropertyName => _mapping.KeyProperties.FirstOrDefault()?.Name;
    public PropertyInfo? KeyProperty => _mapping.KeyProperties.FirstOrDefault();

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
        _keyColumns = GetKeyColumns();
        _insertableProperties = [.. _mapping.PropertyMappings.Where(pm => !pm.IsGenerated || pm.UsesSequence)];
        _updatableProperties = [.. _mapping.PropertyMappings.Where(pm => !_mapping.KeyProperties.Contains(pm.Property) && !pm.IsGenerated)];

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
        if (_keyColumns.Length == 0 || KeyPropertyName is null)
            return string.Empty;

        var predicates = _mapping.KeyProperties
            .Select(p =>
            {
                var column = GetColumnNameForProperty(p);
                var param = _dialect.FormatParameter(p.Name);
                return $"{_dialect.QuoteIdentifier(column)} = {param}";
            });

        return $"{SelectAllSql} WHERE {string.Join(" AND ", predicates)}";
    }

    private (string insert, string? insertReturningId, string update, string delete) BuildMutatingSql()
    {
        EnsureNotReadOnly();
        EnsureHasKey();

        var insert = BuildInsertSql();
        var insertReturningId = BuildInsertReturningIdSql(insert);
        var update = BuildUpdateSql();
        var delete = BuildDeleteSql();

        return (insert, insertReturningId, update, delete);
    }

    private string BuildInsertSql()
    {
        if (_insertableProperties.Length == 0)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' has no insertable columns. " +
                "All properties are marked as DatabaseGenerated or NotMapped.");
        }

        var columns = string.Join(", ", _insertableProperties.Select(pm => _dialect.QuoteIdentifier(pm.ColumnName)));
        var values = string.Join(", ", _insertableProperties.Select(pm =>
        {
            if (!string.IsNullOrWhiteSpace(pm.SequenceName))
            {
                return $"{_dialect.QuoteIdentifier(pm.SequenceName!)}.NEXTVAL";
            }
            return _dialect.FormatParameter(pm.Property.Name);
        }));

        return $"INSERT INTO {_fullTableName} ({columns}) VALUES ({values})";
    }

    private string? BuildInsertReturningIdSql(string insertSql)
    {
        if (_keyColumns.Length == 0)
            return null;

        try
        {
            return _dialect.BuildInsertReturningId(insertSql, _fullTableName, _keyColumns);
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private string BuildUpdateSql()
    {
        if (_updatableProperties.Length == 0)
        {
            // No updatable columns; skip generating update SQL so UpdateAsync will surface a clear error.
            return string.Empty;
        }

        var setClause = string.Join(
            ", ",
            _updatableProperties.Select(pm =>
                $"{_dialect.QuoteIdentifier(pm.ColumnName)} = {_dialect.FormatParameter(pm.Property.Name)}"));

        var keyPredicate = BuildKeyPredicate();
        return $"UPDATE {_fullTableName} SET {setClause} WHERE {keyPredicate}";
    }

    private string BuildDeleteSql()
    {
        var keyPredicate = BuildKeyPredicate();
        return $"DELETE FROM {_fullTableName} WHERE {keyPredicate}";
    }

    private void EnsureNotReadOnly()
    {
        if (_mapping.IsReadOnly)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' is marked as ReadOnly and cannot be modified.");
        }
    }

    private void EnsureHasKey()
    {
        if (_mapping.KeyProperties.Count == 0 || _keyColumns.Length == 0 || KeyPropertyName is null)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' has no key configured for mutations.");
        }
    }

    private string[] GetKeyColumns()
    {
        if (_mapping.KeyProperties.Count == 0)
            return Array.Empty<string>();

        var list = new List<string>();
        foreach (var kp in _mapping.KeyProperties)
        {
            var map = _mapping.PropertyMappings.FirstOrDefault(pm => pm.Property == kp)
                ?? throw new InvalidOperationException($"Key property '{kp.Name}' has no mapping.");
            list.Add(map.ColumnName);
        }
        return list.ToArray();
    }

    private string BuildKeyPredicate()
    {
        var predicates = _mapping.KeyProperties.Select(p =>
        {
            var column = GetColumnNameForProperty(p);
            var param = _dialect.FormatParameter(p.Name);
            return $"{_dialect.QuoteIdentifier(column)} = {param}";
        });

        return string.Join(" AND ", predicates);
    }

    private string GetColumnNameForProperty(PropertyInfo property)
    {
        var map = _mapping.PropertyMappings.FirstOrDefault(pm => pm.Property == property)
            ?? throw new InvalidOperationException($"No mapping found for property '{property.Name}'.");

        return map.ColumnName;
    }
}
