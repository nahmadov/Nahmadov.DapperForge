using System.Linq.Expressions;
using Nahmadov.DapperForge.Core.Interfaces;

namespace Nahmadov.DapperForge.Core.Context;

    internal sealed class IncludableQueryable<TEntity, TProperty> : IIncludableQueryable<TEntity, TProperty>
    where TEntity : class
{
    private readonly DapperQueryable<TEntity> _inner;

    public IncludableQueryable(DapperQueryable<TEntity> inner) => _inner = inner;

    public IDapperQueryable<TEntity> Where(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false)
        => _inner.Where(predicate, ignoreCase);

    public IDapperQueryable<TEntity> OrderBy(Expression<Func<TEntity, object?>> keySelector)
        => _inner.OrderBy(keySelector);

    public IDapperQueryable<TEntity> OrderByDescending(Expression<Func<TEntity, object?>> keySelector)
        => _inner.OrderByDescending(keySelector);

    public IDapperQueryable<TEntity> Skip(int count) => _inner.Skip(count);
    public IDapperQueryable<TEntity> Take(int count) => _inner.Take(count);

    public IDapperQueryable<TEntity> AsSplitQuery() => _inner.AsSplitQuery();
    public IDapperQueryable<TEntity> AsSingleQuery() => _inner.AsSingleQuery();
    public IDapperQueryable<TEntity> AsNoIdentityResolution() => _inner.AsNoIdentityResolution();

    public IIncludableQueryable<TEntity, TRelated?> Include<TRelated>(Expression<Func<TEntity, TRelated?>> navigationSelector)
        where TRelated : class
        => _inner.Include(navigationSelector);

    public IIncludableQueryable<TEntity, IEnumerable<TRelated>> Include<TRelated>(Expression<Func<TEntity, IEnumerable<TRelated>>> navigationSelector)
        where TRelated : class
        => _inner.Include(navigationSelector);

    public IIncludableQueryable<TEntity, TNext?> ThenInclude<TPrevious, TNext>(Expression<Func<TPrevious, TNext?>> navigationSelector)
        where TPrevious : class
        where TNext : class
        => _inner.ThenInclude(navigationSelector);

    public IIncludableQueryable<TEntity, IEnumerable<TNext>> ThenInclude<TPrevious, TNext>(Expression<Func<TPrevious, IEnumerable<TNext>>> navigationSelector)
        where TPrevious : class
        where TNext : class
        => _inner.ThenInclude(navigationSelector);

    public Task<IEnumerable<TEntity>> ToListAsync() => _inner.ToListAsync();
    public Task<TEntity?> FirstOrDefaultAsync() => _inner.FirstOrDefaultAsync();
    public Task<TEntity?> SingleOrDefaultAsync() => _inner.SingleOrDefaultAsync();
    public Task<long> CountAsync() => _inner.CountAsync();
}