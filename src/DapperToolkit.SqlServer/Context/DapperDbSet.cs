using System.Linq.Expressions;
using System.Reflection;

using Dapper;

using DapperToolkit.Core.Attributes;
using DapperToolkit.Core.Interfaces;
using DapperToolkit.Core.Mapping;

namespace DapperToolkit.SqlServer.Context;

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

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        var visitor = new SqlServerPredicateVisitor();
        var (whereClause, parameters) = visitor.Translate(predicate.Body);

        var sql = $"SELECT {DapperDbSet<T>.GetProjection()} FROM {_tableName} WHERE {whereClause}";
        return await _context.QueryFirstOrDefaultAsync<T>(sql, parameters);
    }

    public Task<int> InsertAsync(T entity)
    {
        throw new NotImplementedException();
    }

    public Task<int> UpdateAsync(T entity)
    {
        throw new NotImplementedException();
    }

    public async Task<int> DeleteAsync(int id)
    {
        var sql = $"DELETE FROM {_tableName} WHERE Id = :Id";
        return await _context.ExecuteAsync(sql, new { Id = id });
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