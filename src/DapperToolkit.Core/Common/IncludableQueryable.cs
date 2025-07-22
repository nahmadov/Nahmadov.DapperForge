using System.Linq.Expressions;
using DapperToolkit.Core.Interfaces;

namespace DapperToolkit.Core.Common;

public class IncludableQueryable<T, TProperty> : IIncludableQueryable<T, TProperty> where T : class
{
    private readonly IDapperDbSet<T> _dbSet;
    private readonly List<IncludeInfo> _includes;

    public IncludableQueryable(IDapperDbSet<T> dbSet, List<IncludeInfo> includes)
    {
        _dbSet = dbSet;
        _includes = includes;
    }

    public async Task<IEnumerable<T>> ToListAsync()
    {
        if (_dbSet is IIncludableDbSet<T> includableDbSet)
        {
            return await includableDbSet.ExecuteWithIncludesAsync(_includes);
        }
        
        throw new InvalidOperationException("DbSet does not support ThenInclude operations");
    }

    public IIncludableQueryable<T, TNextProperty> ThenInclude<TNextProperty>(Expression<Func<TProperty, TNextProperty>> navigationPropertyPath)
    {
        var newInclude = new IncludeInfo
        {
            NavigationExpression = navigationPropertyPath,
            ParentType = typeof(TProperty),
            PropertyType = typeof(TNextProperty),
            IsCollection = false
        };

        var newIncludes = new List<IncludeInfo>(_includes) { newInclude };
        return new IncludableQueryable<T, TNextProperty>(_dbSet, newIncludes);
    }

    public IIncludableQueryable<T, TNextProperty> ThenInclude<TNextProperty>(Expression<Func<TProperty, IEnumerable<TNextProperty>>> navigationPropertyPath)
    {
        var newInclude = new IncludeInfo
        {
            NavigationExpression = navigationPropertyPath,
            ParentType = typeof(TProperty),
            PropertyType = typeof(TNextProperty),
            IsCollection = true
        };

        var newIncludes = new List<IncludeInfo>(_includes) { newInclude };
        return new IncludableQueryable<T, TNextProperty>(_dbSet, newIncludes);
    }
}

public class IncludeInfo
{
    public LambdaExpression NavigationExpression { get; set; } = null!;
    public Type ParentType { get; set; } = null!;
    public Type PropertyType { get; set; } = null!;
    public bool IsCollection { get; set; }
    public Expression<Func<object, bool>>? Predicate { get; set; }
}

public interface IIncludableDbSet<T> : IDapperDbSet<T> where T : class
{
    Task<IEnumerable<T>> ExecuteWithIncludesAsync(List<IncludeInfo> includes);
}