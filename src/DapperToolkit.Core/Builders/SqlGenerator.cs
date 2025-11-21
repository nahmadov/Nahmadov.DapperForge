using DapperToolkit.Core.Mapping;

namespace DapperToolkit.Core.Builders;

internal sealed class SqlGenerator<TEntity> where TEntity : class
{
    private readonly EntityMapping _m;

    public string TableName => _m.TableName;
    public string KeyPropertyName => _m.KeyProperty.Name;

    public string SelectAllSql { get; }
    public string SelectByIdSql { get; }
    public string InsertSql { get; }
    public string UpdateSql { get; }
    public string DeleteByIdSql { get; }

    public SqlGenerator()
    {
        _m = EntityMappingCache<TEntity>.Mapping;

        string fullTableName = _m.Schema is null ? _m.TableName : $"{_m.Schema}.{_m.TableName}";

        var columnNames = _m.Properties.Select(p => p.Name).ToArray();
        var nonKeyProps = _m.Properties.Where(p => p != _m.KeyProperty).ToArray();

        string keyColumn = _m.PropertyMappings.First(pm => pm.Property == _m.KeyProperty).ColumnName;

        // SELECT
        var columnList = string.Join(", ", columnNames);
        SelectAllSql = $"SELECT {columnList} FROM {_m.TableName}";
        SelectByIdSql = $"{SelectAllSql} WHERE {keyColumn} = @{_m.KeyProperty.Name}";

        // INSERT
        var insertColumns = string.Join(", ", _m.PropertyMappings.Select(pm => pm.ColumnName));
        var insertParams = string.Join(", ", _m.PropertyMappings.Select(pm => "@" + pm.Property.Name));
        InsertSql = $"INSERT INTO {_m.TableName} ({insertColumns}) VALUES ({insertParams})";

        // UPDATE
        var setClause = string.Join(", ", _m.PropertyMappings.Where(pm => pm.Property != _m.KeyProperty).Select(pm => $"{pm.ColumnName} = @{pm.Property.Name}"));
        UpdateSql = $"UPDATE {_m.TableName} SET {setClause} WHERE {_m.KeyProperty.Name} = @{_m.KeyProperty.Name}";

        // DELETE
        DeleteByIdSql = $"DELETE FROM {fullTableName} WHERE {keyColumn} = @{_m.KeyProperty.Name}";
    }
}