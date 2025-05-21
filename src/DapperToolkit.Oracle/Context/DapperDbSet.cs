using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.Oracle.Context;

public class DapperDbSet<T>(DapperDbContext context) : IDapperDbSet<T> where T : class
{
    private readonly DapperDbContext _context = context;
    private readonly string _tableName = typeof(T).Name;

  public async Task<IEnumerable<T>> GetAllAsync()
    {
        var sql = $"SELECT * FROM {_tableName}";
        return await _context.QueryAsync<T>(sql);
    }

    public async Task<T?> GetByIdAsync(int id)
    {
        var sql = $"SELECT * FROM {_tableName} WHERE Id = :Id";
        return await _context.QueryFirstOrDefaultAsync<T>(sql, new { Id = id });
    }

    public Task<int> InsertAsync(T entity)
    {
        throw new NotImplementedException("Property mapping generator əlavə olunmalıdır.");
    }

    public Task<int> UpdateAsync(T entity)
    {
        throw new NotImplementedException("Property mapping generator əlavə olunmalıdır.");
    }

    public async Task<int> DeleteAsync(int id)
    {
        var sql = $"DELETE FROM {_tableName} WHERE Id = :Id";
        return await _context.ExecuteAsync(sql, new { Id = id });
    }
}