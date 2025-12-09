using System.Reflection;

using Nahmadov.DapperForge.Core.Interfaces;
using Nahmadov.DapperForge.Core.Mapping;

namespace Nahmadov.DapperForge.Core.Builders;

/// <summary>
/// Generates SQL statements for CRUD operations based on entity mappings and a SQL dialect.
/// </summary>
internal sealed class SqlGenerator<TEntity> where TEntity : class
{
    private readonly ISqlDialect _dialect;
    private readonly EntityMapping _mapping;
    private readonly string _fullTableName;
    private readonly string[] _keyColumns;
    private readonly PropertyMapping[] _insertableProperties;
    private readonly PropertyMapping[] _updatableProperties;

    /// <summary>
    /// Gets the mapped table name.
    /// </summary>
    public string TableName => _mapping.TableName;

    /// <summary>
    /// Gets the name of the key property if present.
    /// </summary>
    public string? KeyPropertyName => _mapping.KeyProperties.FirstOrDefault()?.Name;

    /// <summary>
    /// Gets metadata for the key property if present.
    /// </summary>
    public PropertyInfo? KeyProperty => _mapping.KeyProperties.FirstOrDefault();

    /// <summary>
    /// Indicates whether the key property is an identity column.
    /// </summary>
    public bool IsKeyIdentity =>
        _mapping.KeyProperty is not null && (_mapping.PropertyMappings
                .FirstOrDefault(pm => pm.Property == _mapping.KeyProperty)?.IsIdentity ?? false);

    /// <summary>
    /// Gets the name of the SQL dialect in use.
    /// </summary>
    public string DialectName => _dialect.Name;

    /// <summary>
    /// SELECT statement that returns all rows.
    /// </summary>
    public string SelectAllSql { get; }

    /// <summary>
    /// SELECT statement that returns a row by primary key.
    /// </summary>
    public string SelectByIdSql { get; }

    /// <summary>
    /// INSERT statement for the entity.
    /// </summary>
    public string InsertSql { get; }

    /// <summary>
    /// INSERT statement that also returns the generated key, when supported.
    /// </summary>
    public string? InsertReturningIdSql { get; }

    /// <summary>
    /// UPDATE statement for the entity.
    /// </summary>
    public string UpdateSql { get; }

    /// <summary>
    /// DELETE statement that removes a row by key.
    /// </summary>
    public string DeleteByIdSql { get; }

    /// <summary>
    /// Gets the SQL dialect instance used by this generator.
    /// </summary>
    public ISqlDialect Dialect => _dialect;

    /// <summary>
    /// Initializes a new generator for the provided mapping and dialect.
    /// </summary>
    /// <param name="dialect">SQL dialect used to format identifiers and parameters.</param>
    /// <param name="mapping">Entity mapping metadata.</param>
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

    /// <summary>
    /// Builds a fully qualified and quoted table name including schema if supplied.
    /// </summary>
    /// <returns>Quoted table name with optional schema prefix.</returns>
    private string BuildFullTableName()
    {
        var table = _dialect.QuoteIdentifier(_mapping.TableName);

        if (string.IsNullOrWhiteSpace(_mapping.Schema))
            return table;

        return $"{_dialect.QuoteIdentifier(_mapping.Schema!)}.{table}";
    }

    /// <summary>
    /// Builds a SELECT statement that projects all mapped properties.
    /// </summary>
    /// <returns>Complete SELECT statement.</returns>
    private string BuildSelectAllSql()
    {
        var columnList = string.Join(
            ", ",
            _mapping.PropertyMappings.Select(pm =>
                $"{_dialect.QuoteIdentifier(pm.ColumnName)} AS {_dialect.QuoteIdentifier(pm.Property.Name)}"));

        return $"SELECT {columnList} FROM {_fullTableName}";
    }

    /// <summary>
    /// Builds a SELECT statement filtered by the configured key columns.
    /// </summary>
    /// <returns>SELECT by key SQL or an empty string if no key exists.</returns>
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

    /// <summary>
    /// Builds INSERT, INSERT returning key, UPDATE, and DELETE SQL statements.
    /// </summary>
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

    /// <summary>
    /// Creates the INSERT statement for the entity including sequence usage when configured.
    /// </summary>
    /// <returns>INSERT SQL string.</returns>
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

    /// <summary>
    /// Attempts to create an INSERT statement that returns the generated key values.
    /// </summary>
    /// <param name="insertSql">Base INSERT statement.</param>
    /// <returns>Dialect-specific INSERT-returning SQL or null when unsupported.</returns>
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

    /// <summary>
    /// Builds the UPDATE statement for the entity based on updatable properties.
    /// </summary>
    /// <returns>UPDATE SQL string or empty if no updatable columns exist.</returns>
    private string BuildUpdateSql()
    {
        if (_updatableProperties.Length == 0)
        {
            return string.Empty;
        }

        var setClause = string.Join(
            ", ",
            _updatableProperties.Select(pm =>
                $"{_dialect.QuoteIdentifier(pm.ColumnName)} = {_dialect.FormatParameter(pm.Property.Name)}"));

        var keyPredicate = BuildKeyPredicate();
        return $"UPDATE {_fullTableName} SET {setClause} WHERE {keyPredicate}";
    }

    /// <summary>
    /// Builds the DELETE statement filtering by key columns.
    /// </summary>
    /// <returns>DELETE SQL string.</returns>
    private string BuildDeleteSql()
    {
        var keyPredicate = BuildKeyPredicate();
        return $"DELETE FROM {_fullTableName} WHERE {keyPredicate}";
    }

    /// <summary>
    /// Throws if the entity is marked read-only.
    /// </summary>
    private void EnsureNotReadOnly()
    {
        if (_mapping.IsReadOnly)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' is marked as ReadOnly and cannot be modified.");
        }
    }

    /// <summary>
    /// Throws if the entity lacks key information required for mutations.
    /// </summary>
    private void EnsureHasKey()
    {
        if (_mapping.KeyProperties.Count == 0 || _keyColumns.Length == 0 || KeyPropertyName is null)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' has no key configured for mutations.");
        }
    }

    /// <summary>
    /// Gets column names corresponding to key properties.
    /// </summary>
    /// <returns>Array of key column names or an empty array.</returns>
    private string[] GetKeyColumns()
    {
        if (_mapping.KeyProperties.Count == 0)
            return [];

        var list = new List<string>();
        foreach (var kp in _mapping.KeyProperties)
        {
            var map = _mapping.PropertyMappings.FirstOrDefault(pm => pm.Property == kp)
                ?? throw new InvalidOperationException($"Key property '{kp.Name}' has no mapping.");
            list.Add(map.ColumnName);
        }
        return [.. list];
    }

    /// <summary>
    /// Builds an equality predicate combining all key columns.
    /// </summary>
    /// <returns>Predicate string joined with AND.</returns>
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

    /// <summary>
    /// Resolves the mapped column name for the specified property.
    /// </summary>
    /// <param name="property">Property to resolve.</param>
    /// <returns>Column name defined in the mapping.</returns>
    private string GetColumnNameForProperty(PropertyInfo property)
    {
        var map = _mapping.PropertyMappings.FirstOrDefault(pm => pm.Property == property)
            ?? throw new InvalidOperationException($"No mapping found for property '{property.Name}'.");

        return map.ColumnName;
    }
}
