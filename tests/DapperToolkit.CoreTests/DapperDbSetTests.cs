using System.Data;
using System.Linq.Expressions;

using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.CoreTests;

public class DapperDbSetTests
{
    private class FakeDbSet<T> : IDapperDbSet<T> where T : class, new()
    {
        public Task<IEnumerable<T>> ToListAsync() => Task.FromResult<IEnumerable<T>>([new T()]);

        public Task<IEnumerable<T>> ToListAsync(Expression<Func<T, object>> orderBy, bool ascending = true) => Task.FromResult<IEnumerable<T>>([new T()]);

        public Task<IEnumerable<T>> WhereAsync(Expression<Func<T, bool>> predicate) => Task.FromResult<IEnumerable<T>>([new T()]);

        public Task<IEnumerable<TResult>> SelectAsync<TResult>(Expression<Func<T, TResult>> selector) => Task.FromResult<IEnumerable<TResult>>([default(TResult)!]);

        public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate) => Task.FromResult<T?>(new T());

        public Task<int> InsertAsync(T entity, IDbTransaction? transaction = null) => Task.FromResult(1);

        public Task<int> UpdateAsync(T entity, IDbTransaction? transaction = null) => Task.FromResult(1);

        public Task<int> DeleteAsync(int id, IDbTransaction? transaction = null) => Task.FromResult(1);

        public Task<int> DeleteAsync(Expression<Func<T, bool>> predicate, IDbTransaction? transaction = null) => Task.FromResult(1);

        public Task<int> DeleteAsync(T entity, IDbTransaction? transaction = null) => Task.FromResult(1);

        public Task<bool> AnyAsync() => Task.FromResult(true);

        public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate) => Task.FromResult(true);

        public Task<bool> ExistsAsync(int id) => Task.FromResult(true);

        public Task<int> CountAsync() => Task.FromResult(5);

        public Task<int> CountAsync(Expression<Func<T, bool>> predicate) => Task.FromResult(3);

        public Task<IEnumerable<T>> PageAsync(int pageNumber, int pageSize) => Task.FromResult<IEnumerable<T>>([new T(), new T()]);

        public Task<IEnumerable<T>> PageAsync(int pageNumber, int pageSize, Expression<Func<T, object>> orderBy, bool ascending = true) => Task.FromResult<IEnumerable<T>>([new T(), new T()]);

        public Task<(IEnumerable<T> Data, int TotalCount)> PageWithCountAsync(int pageNumber, int pageSize) => Task.FromResult<(IEnumerable<T>, int)>(([new T(), new T()], 10));

        public Task<(IEnumerable<T> Data, int TotalCount)> PageWithCountAsync(int pageNumber, int pageSize, Expression<Func<T, object>> orderBy, bool ascending = true) => Task.FromResult<(IEnumerable<T>, int)>(([new T(), new T()], 10));

        public Task<IEnumerable<T>> IncludeAsync<TProperty>(Expression<Func<T, TProperty>> includeExpression) => Task.FromResult<IEnumerable<T>>([new T()]);

        public Task<IEnumerable<T>> IncludeAsync<TProperty>(Expression<Func<T, bool>> predicate, Expression<Func<T, TProperty>> includeExpression) => Task.FromResult<IEnumerable<T>>([new T()]);

        public IIncludableQueryable<T, TProperty> Include<TProperty>(Expression<Func<T, TProperty>> includeExpression) => throw new NotImplementedException();

        public IIncludableQueryable<T, TProperty> Include<TProperty>(Expression<Func<T, bool>> predicate, Expression<Func<T, TProperty>> includeExpression) => throw new NotImplementedException();

        public Task<int> BulkInsertAsync(IEnumerable<T> entities, IDbTransaction? transaction = null) => Task.FromResult(entities?.Count() ?? 0);

        public Task<int> BulkUpdateAsync(IEnumerable<T> entities, IDbTransaction? transaction = null) => Task.FromResult(entities?.Count() ?? 0);

        public Task<int> BulkDeleteAsync(IEnumerable<T> entities, IDbTransaction? transaction = null) => Task.FromResult(entities?.Count() ?? 0);

        public Task<int> BulkDeleteAsync(IEnumerable<int> ids, IDbTransaction? transaction = null) => Task.FromResult(ids?.Count() ?? 0);
    }

    private class SampleEntity
    {
        public int Id { get; set; }
    }

    [Fact]
    public async Task Should_Execute_All_Methods()
    {
        var dbSet = new FakeDbSet<SampleEntity>();

        var all = await dbSet.ToListAsync();
        Assert.Single(all);

        var one = await dbSet.FirstOrDefaultAsync(x => x.Id == 1);
        Assert.NotNull(one);

        var inserted = await dbSet.InsertAsync(new SampleEntity());
        Assert.Equal(1, inserted);

        var updated = await dbSet.UpdateAsync(new SampleEntity());
        Assert.Equal(1, updated);

        var deleted = await dbSet.DeleteAsync(1);
        Assert.Equal(1, deleted);

        var deletedByPredicate = await dbSet.DeleteAsync(x => x.Id == 1);
        Assert.Equal(1, deletedByPredicate);

        var deletedByEntity = await dbSet.DeleteAsync(new SampleEntity { Id = 1 });
        Assert.Equal(1, deletedByEntity);

        var any = await dbSet.AnyAsync();
        Assert.True(any);

        var anyWithPredicate = await dbSet.AnyAsync(x => x.Id == 1);
        Assert.True(anyWithPredicate);

        var count = await dbSet.CountAsync();
        Assert.Equal(5, count);

        var countWithPredicate = await dbSet.CountAsync(x => x.Id == 1);
        Assert.Equal(3, countWithPredicate);

        var pagedData = await dbSet.PageAsync(1, 2);
        Assert.Equal(2, pagedData.Count());

        var pagedWithCount = await dbSet.PageWithCountAsync(1, 2);
        Assert.Equal(2, pagedWithCount.Data.Count());
        Assert.Equal(10, pagedWithCount.TotalCount);
    }
}