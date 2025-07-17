using System.Linq.Expressions;

namespace DapperToolkit.Core.Interfaces;

public interface IDapperDbSet<T> where T : class
{
    Task<IEnumerable<T>> ToListAsync();
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
    Task<int> InsertAsync(T entity);
    Task<int> UpdateAsync(T entity);
    Task<int> DeleteAsync(int id);
    Task<int> DeleteAsync(Expression<Func<T, bool>> predicate);
    Task<int> DeleteAsync(T entity);
    Task<bool> AnyAsync();
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
}