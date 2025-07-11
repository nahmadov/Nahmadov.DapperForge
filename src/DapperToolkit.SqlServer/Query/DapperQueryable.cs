using System.Linq.Expressions;

using DapperToolkit.SqlServer.Context;

namespace DapperToolkit.SqlServer.Query;

public class DapperQueryable<T>(DapperDbContext context, Expression<Func<T, bool>>? predicate = null) where T : class
{
    private readonly Expression<Func<T, bool>>? _predicate = predicate;
    private readonly DapperDbContext _context = context;

    public DapperQueryable<T> Where(Expression<Func<T, bool>> predicate)
    {
        var combined = _predicate == null
            ? predicate
            : Expression.Lambda<Func<T, bool>>(
                Expression.AndAlso(_predicate.Body, predicate.Body),
                predicate.Parameters);

        return new DapperQueryable<T>(_context, combined);
    }

    public async Task<List<T>> ToListAsync()
    {
        var dbSet = new DapperDbSet<T>(_context);
        if (_predicate is null)
            return (await dbSet.ToListAsync()).ToList();

        var item = await dbSet.FirstOrDefaultAsync(_predicate);
        if (item is not null)
            return [item];

        return [];
    }

    public DapperQueryable<T> AsQueryable()
    {
        return new DapperQueryable<T>(_context);
    }
}