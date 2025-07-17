using System.Linq.Expressions;

using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.CoreTests;

public class DapperDbSetTests
{
    private class FakeDbSet<T> : IDapperDbSet<T> where T : class, new()
    {
        public Task<IEnumerable<T>> ToListAsync() => Task.FromResult<IEnumerable<T>>([new T()]);

        public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate) => Task.FromResult<T?>(new T());

        public Task<int> InsertAsync(T entity) => Task.FromResult(1);

        public Task<int> UpdateAsync(T entity) => Task.FromResult(1);

        public Task<int> DeleteAsync(int id) => Task.FromResult(1);

        public Task<int> DeleteAsync(Expression<Func<T, bool>> predicate) => Task.FromResult(1);

        public Task<int> DeleteAsync(T entity) => Task.FromResult(1);

        public Task<bool> AnyAsync() => Task.FromResult(true);

        public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate) => Task.FromResult(true);
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
    }
}