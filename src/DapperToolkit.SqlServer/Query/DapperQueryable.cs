using System.Linq.Expressions;

using DapperToolkit.SqlServer.Context;

namespace DapperToolkit.SqlServer.Query;

public class DapperQueryable<T>(DapperDbContext context, Expression<Func<T, bool>>? predicate = null, Expression<Func<T, object>>? orderBy = null, bool ascending = true) where T : class
{
    private readonly Expression<Func<T, bool>>? _predicate = predicate;
    private readonly Expression<Func<T, object>>? _orderBy = orderBy;
    private readonly bool _ascending = ascending;
    private readonly DapperDbContext _context = context;

    public DapperQueryable<T> Where(Expression<Func<T, bool>> predicate)
    {
        var combined = _predicate == null
            ? predicate
            : Expression.Lambda<Func<T, bool>>(
                Expression.AndAlso(_predicate.Body, predicate.Body),
                predicate.Parameters);

        return new DapperQueryable<T>(_context, combined, _orderBy, _ascending);
    }

    public DapperQueryable<T> OrderBy(Expression<Func<T, object>> orderBy)
    {
        return new DapperQueryable<T>(_context, _predicate, orderBy, true);
    }

    public DapperQueryable<T> OrderByDescending(Expression<Func<T, object>> orderBy)
    {
        return new DapperQueryable<T>(_context, _predicate, orderBy, false);
    }

    public async Task<List<T>> ToListAsync()
    { 
        if (_predicate is null && _orderBy is null)
        {
            var dbSet = new DapperDbSet<T>(_context);
            return (await dbSet.ToListAsync()).ToList();
        }
            
        if (_predicate is null && _orderBy is not null)
        {
            var dbSet = new DapperDbSet<T>(_context);
            return (await dbSet.ToListAsync(_orderBy, _ascending)).ToList();
        }

        if (_predicate is not null && _orderBy is null)
        {

            var dbSet = new DapperDbSet<T>(_context);
            var allItems = await dbSet.ToListAsync();
            var compiledPredicate = _predicate.Compile();
            return allItems.Where(compiledPredicate).ToList();
        }

        if (_predicate is not null && _orderBy is not null)
        {
            var dbSet = new DapperDbSet<T>(_context);
            var allItems = await dbSet.ToListAsync(_orderBy, _ascending);
            var compiledPredicate = _predicate.Compile();
            return allItems.Where(compiledPredicate).ToList();
        }

        return [];
    }

    public DapperQueryable<T> AsQueryable()
    {
        return new DapperQueryable<T>(_context);
    }
}