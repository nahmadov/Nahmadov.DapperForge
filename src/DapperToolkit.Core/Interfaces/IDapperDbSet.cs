using System.Data;
using System.Linq.Expressions;

namespace DapperToolkit.Core.Interfaces;

public interface IDapperDbSet<T> where T : class
{
    Task<IEnumerable<T>> ToListAsync();
    Task<IEnumerable<T>> ToListAsync(Expression<Func<T, object>> orderBy, bool ascending = true);
    Task<IEnumerable<T>> WhereAsync(Expression<Func<T, bool>> predicate);
    Task<IEnumerable<TResult>> SelectAsync<TResult>(Expression<Func<T, TResult>> selector);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
    Task<int> InsertAsync(T entity, IDbTransaction? transaction = null);
    Task<int> UpdateAsync(T entity, IDbTransaction? transaction = null);
    Task<int> DeleteAsync(int id, IDbTransaction? transaction = null);
    Task<int> DeleteAsync(Expression<Func<T, bool>> predicate, IDbTransaction? transaction = null);
    Task<int> DeleteAsync(T entity, IDbTransaction? transaction = null);
    Task<bool> AnyAsync();
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
    Task<bool> ExistsAsync(int id);
    Task<int> CountAsync();
    Task<int> CountAsync(Expression<Func<T, bool>> predicate);
    Task<IEnumerable<T>> PageAsync(int pageNumber, int pageSize);
    Task<IEnumerable<T>> PageAsync(int pageNumber, int pageSize, Expression<Func<T, object>> orderBy, bool ascending = true);
    Task<(IEnumerable<T> Data, int TotalCount)> PageWithCountAsync(int pageNumber, int pageSize);
    Task<(IEnumerable<T> Data, int TotalCount)> PageWithCountAsync(int pageNumber, int pageSize, Expression<Func<T, object>> orderBy, bool ascending = true);
    Task<IEnumerable<T>> IncludeAsync<TProperty>(Expression<Func<T, TProperty>> includeExpression);
    Task<IEnumerable<T>> IncludeAsync<TProperty>(Expression<Func<T, bool>> predicate, Expression<Func<T, TProperty>> includeExpression);
    IIncludableQueryable<T, TProperty> Include<TProperty>(Expression<Func<T, TProperty>> includeExpression);
    IIncludableQueryable<T, TProperty> Include<TProperty>(Expression<Func<T, bool>> predicate, Expression<Func<T, TProperty>> includeExpression);
    
    Task<int> BulkInsertAsync(IEnumerable<T> entities, IDbTransaction? transaction = null);
    Task<int> BulkUpdateAsync(IEnumerable<T> entities, IDbTransaction? transaction = null);
    Task<int> BulkDeleteAsync(IEnumerable<T> entities, IDbTransaction? transaction = null);
    Task<int> BulkDeleteAsync(IEnumerable<int> ids, IDbTransaction? transaction = null);
}
