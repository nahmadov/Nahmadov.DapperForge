using DapperToolkit.Core.Builders;

namespace DapperToolkit.Core.Context;

public sealed class DapperSet<TEntity> where TEntity : class
{
    private readonly DapperDbContext _context;
    private static readonly SqlGenerator<TEntity> _sql = SqlGeneratorCache<TEntity>.Instance;

    internal DapperSet(DapperDbContext context)
    {
        _context = context;
    }

    public Task<IEnumerable<TEntity>> GetAllAsync()
        => _context.QueryAsync<TEntity>(_sql.SelectAllSql);

    public Task<TEntity?> FindAsync(object key)
    {
        var param = new Dictionary<string, object?>
        {
            [_sql.KeyPropertyName] = key
        };

        return _context.QueryFirstOrDefaultAsync<TEntity>(_sql.SelectByIdSql, param);
    }

    public Task<IEnumerable<TEntity>> QueryAsync(string whereClause, object? parameters = null)
    {
        var sql = $"{_sql.SelectAllSql} {whereClause}";
        return _context.QueryAsync<TEntity>(sql, parameters);
    }

    public Task<TEntity?> FirstOrDefaultAsync(string whereClause, object? parameters = null)
    {
        var sql = $"{_sql.SelectAllSql} {whereClause}";
        return _context.QueryFirstOrDefaultAsync<TEntity>(sql, parameters);
    }

    public Task<int> InsertAsync(TEntity entity)
        => _context.ExecuteAsync(_sql.InsertSql, entity);

    public Task<int> UpdateAsync(TEntity entity)
        => _context.ExecuteAsync(_sql.UpdateSql, entity);

    public Task<int> DeleteAsync(TEntity entity)
        => _context.ExecuteAsync(_sql.DeleteByIdSql, entity);

    public Task<int> DeleteByIdAsync(object key)
    {
        var param = new Dictionary<string, object?>
        {
            [_sql.KeyPropertyName] = key
        };

        return _context.ExecuteAsync(_sql.DeleteByIdSql, param);
    }
}