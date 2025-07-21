using System.Linq.Expressions;
using System.Reflection;

using Dapper;

using DapperToolkit.Core.Attributes;
using DapperToolkit.Core.Interfaces;
using DapperToolkit.Core.Mapping;

namespace DapperToolkit.Oracle.Context;

public class DapperDbSet<T> : IDapperDbSet<T> where T : class
{
    private readonly DapperDbContext _context;
    private readonly string _tableName;
    public DapperDbSet(DapperDbContext context)
    {
        _context = context;

        var tableAttr = typeof(T).GetCustomAttribute<TableNameAttribute>();
        _tableName = tableAttr?.Name ?? typeof(T).Name;
        SqlMapper.SetTypeMap(typeof(T), new ColumnAttributeTypeMapper<T>());
    }

    public async Task<IEnumerable<T>> ToListAsync()
    {
        var sql = $"SELECT {DapperDbSet<T>.GetProjection()} FROM {_tableName}";
        return await _context.QueryAsync<T>(sql);
    }

    public async Task<IEnumerable<T>> ToListAsync(Expression<Func<T, object>> orderBy, bool ascending = true)
    {
        var orderByVisitor = new OracleOrderByVisitor();
        var orderByClause = orderByVisitor.TranslateOrderBy(orderBy, ascending);
        
        var sql = $"SELECT {DapperDbSet<T>.GetProjection()} FROM {_tableName} ORDER BY {orderByClause}";
        return await _context.QueryAsync<T>(sql);
    }

    public async Task<IEnumerable<T>> WhereAsync(Expression<Func<T, bool>> predicate)
    {
        var visitor = new OraclePredicateVisitor();
        var (whereClause, parameters) = visitor.Translate(predicate.Body);

        var sql = $"SELECT {DapperDbSet<T>.GetProjection()} FROM {_tableName} WHERE {whereClause}";
        return await _context.QueryAsync<T>(sql, parameters);
    }

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        var visitor = new OraclePredicateVisitor();
        var (whereClause, parameters) = visitor.Translate(predicate.Body);

        var sql = $"SELECT {DapperDbSet<T>.GetProjection()} FROM {_tableName} WHERE {whereClause}";
        return await _context.QueryFirstOrDefaultAsync<T>(sql, parameters);
    }

    public async Task<int> InsertAsync(T entity)
    {
        var properties = typeof(T).GetProperties()
            .Where(p => p.Name != "Id")
            .ToList();

        var columns = string.Join(", ", properties.Select(p =>
        {
            var attr = p.GetCustomAttribute<ColumnNameAttribute>();
            return attr?.Name ?? p.Name;
        }));

        var values = string.Join(", ", properties.Select(p => $":{p.Name}"));

        var sql = $"INSERT INTO {_tableName} ({columns}) VALUES ({values})";
        return await _context.ExecuteAsync(sql, entity);
    }

  public async Task<int> UpdateAsync(T entity)
  {
    var properties = typeof(T).GetProperties()
        .Where(p => p.Name != "Id")
        .ToList();

    var setClause = string.Join(", ", properties.Select(p =>
    {
      var attr = p.GetCustomAttribute<ColumnNameAttribute>();
      var columnName = attr?.Name ?? p.Name;
      return $"{columnName} = :{p.Name}";
    }));

    var sql = $"UPDATE {_tableName} SET {setClause} WHERE Id = :Id";
    return await _context.ExecuteAsync(sql, entity);
  }

    public async Task<int> DeleteAsync(int id)
    {
        var sql = $"DELETE FROM {_tableName} WHERE Id = :Id";
        return await _context.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<int> DeleteAsync(Expression<Func<T, bool>> predicate)
    {
        var visitor = new OraclePredicateVisitor();
        var (whereClause, parameters) = visitor.Translate(predicate.Body);

        var sql = $"DELETE FROM {_tableName} WHERE {whereClause}";
        return await _context.ExecuteAsync(sql, parameters);
    }

    public async Task<int> DeleteAsync(T entity)
    {
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty == null)
            throw new InvalidOperationException("Entity must have an Id property for deletion.");

        var idValue = idProperty.GetValue(entity);
        if (idValue == null)
            throw new InvalidOperationException("Entity Id cannot be null for deletion.");

        var sql = $"DELETE FROM {_tableName} WHERE Id = :Id";
        return await _context.ExecuteAsync(sql, new { Id = idValue });
    }

    public async Task<bool> AnyAsync()
    {
        var sql = $"SELECT CASE WHEN EXISTS(SELECT 1 FROM {_tableName}) THEN 1 ELSE 0 END FROM DUAL";
        var result = await _context.QueryFirstOrDefaultAsync<int>(sql);
        return result == 1;
    }

    public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
    {
        var visitor = new OraclePredicateVisitor();
        var (whereClause, parameters) = visitor.Translate(predicate.Body);

        var sql = $"SELECT CASE WHEN EXISTS(SELECT 1 FROM {_tableName} WHERE {whereClause}) THEN 1 ELSE 0 END FROM DUAL";
        var result = await _context.QueryFirstOrDefaultAsync<int>(sql, parameters);
        return result == 1;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty == null)
            throw new InvalidOperationException("Entity must have an Id property for existence check.");

        var attr = idProperty.GetCustomAttribute<ColumnNameAttribute>();
        var columnName = attr?.Name ?? "Id";

        var sql = $"SELECT CASE WHEN EXISTS(SELECT 1 FROM {_tableName} WHERE {columnName} = :Id) THEN 1 ELSE 0 END FROM DUAL";
        var result = await _context.QueryFirstOrDefaultAsync<int>(sql, new { Id = id });
        return result == 1;
    }

    public async Task<int> CountAsync()
    {
        var sql = $"SELECT COUNT(*) FROM {_tableName}";
        return await _context.QueryFirstOrDefaultAsync<int>(sql);
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
    {
        var visitor = new OraclePredicateVisitor();
        var (whereClause, parameters) = visitor.Translate(predicate.Body);

        var sql = $"SELECT COUNT(*) FROM {_tableName} WHERE {whereClause}";
        return await _context.QueryFirstOrDefaultAsync<int>(sql, parameters);
    }

    public async Task<IEnumerable<T>> PageAsync(int pageNumber, int pageSize)
    {
        if (pageNumber < 1) throw new ArgumentException("Page number must be greater than 0", nameof(pageNumber));
        if (pageSize < 1) throw new ArgumentException("Page size must be greater than 0", nameof(pageSize));

        var offset = (pageNumber - 1) * pageSize;
        var sql = $"SELECT {GetProjection()} FROM {_tableName} ORDER BY Id OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";
        return await _context.QueryAsync<T>(sql);
    }

    public async Task<IEnumerable<T>> PageAsync(int pageNumber, int pageSize, Expression<Func<T, object>> orderBy, bool ascending = true)
    {
        if (pageNumber < 1) throw new ArgumentException("Page number must be greater than 0", nameof(pageNumber));
        if (pageSize < 1) throw new ArgumentException("Page size must be greater than 0", nameof(pageSize));

        var orderByVisitor = new OracleOrderByVisitor();
        var orderByClause = orderByVisitor.TranslateOrderBy(orderBy, ascending);
        
        var offset = (pageNumber - 1) * pageSize;
        var sql = $"SELECT {GetProjection()} FROM {_tableName} ORDER BY {orderByClause} OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";
        return await _context.QueryAsync<T>(sql);
    }

    public async Task<(IEnumerable<T> Data, int TotalCount)> PageWithCountAsync(int pageNumber, int pageSize)
    {
        if (pageNumber < 1) throw new ArgumentException("Page number must be greater than 0", nameof(pageNumber));
        if (pageSize < 1) throw new ArgumentException("Page size must be greater than 0", nameof(pageSize));

        var offset = (pageNumber - 1) * pageSize;
        var sql = $@"
            SELECT {GetProjection()} FROM {_tableName} ORDER BY Id OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY;
            SELECT COUNT(*) FROM {_tableName};";
        
        using var multi = await _context.Connection.QueryMultipleAsync(sql);
        var data = await multi.ReadAsync<T>();
        var totalCount = await multi.ReadSingleAsync<int>();
        
        return (data, totalCount);
    }

    public async Task<(IEnumerable<T> Data, int TotalCount)> PageWithCountAsync(int pageNumber, int pageSize, Expression<Func<T, object>> orderBy, bool ascending = true)
    {
        if (pageNumber < 1) throw new ArgumentException("Page number must be greater than 0", nameof(pageNumber));
        if (pageSize < 1) throw new ArgumentException("Page size must be greater than 0", nameof(pageSize));

        var orderByVisitor = new OracleOrderByVisitor();
        var orderByClause = orderByVisitor.TranslateOrderBy(orderBy, ascending);
        
        var offset = (pageNumber - 1) * pageSize;
        var sql = $@"
            SELECT {GetProjection()} FROM {_tableName} ORDER BY {orderByClause} OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY;
            SELECT COUNT(*) FROM {_tableName};";
        
        using var multi = await _context.Connection.QueryMultipleAsync(sql);
        var data = await multi.ReadAsync<T>();
        var totalCount = await multi.ReadSingleAsync<int>();
        
        return (data, totalCount);
    }

    private static string GetProjection()
    {
        var projection = "";
        typeof(T).GetProperties().ToList().ForEach(prop =>
        {
            var attr = prop.GetCustomAttribute<ColumnNameAttribute>();
            if (attr != null)
                projection += $"{attr.Name} AS {prop.Name}, ";
            else
                projection += $"{prop.Name}, ";
        });
        projection = projection.TrimEnd(',', ' ');
        return string.IsNullOrEmpty(projection.Trim()) ? "*" : projection;
    }
}
