using System.Linq.Expressions;

namespace DapperToolkit.Core.Interfaces;

public interface IDapperDbSet<T> where T : class
{
    Task<IEnumerable<T>> ToListAsync();
    Task<IEnumerable<T>> ToListAsync(Expression<Func<T, object>> orderBy, bool ascending = true);
    Task<IEnumerable<T>> WhereAsync(Expression<Func<T, bool>> predicate);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
    Task<int> InsertAsync(T entity);
    Task<int> UpdateAsync(T entity);
    Task<int> DeleteAsync(int id);
    Task<int> DeleteAsync(Expression<Func<T, bool>> predicate);
    Task<int> DeleteAsync(T entity);
    Task<bool> AnyAsync();
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
    Task<bool> ExistsAsync(int id);
    Task<int> CountAsync();
    Task<int> CountAsync(Expression<Func<T, bool>> predicate);
    Task<IEnumerable<T>> PageAsync(int pageNumber, int pageSize);
    Task<IEnumerable<T>> PageAsync(int pageNumber, int pageSize, Expression<Func<T, object>> orderBy, bool ascending = true);
    Task<(IEnumerable<T> Data, int TotalCount)> PageWithCountAsync(int pageNumber, int pageSize);
    Task<(IEnumerable<T> Data, int TotalCount)> PageWithCountAsync(int pageNumber, int pageSize, Expression<Func<T, object>> orderBy, bool ascending = true);
}