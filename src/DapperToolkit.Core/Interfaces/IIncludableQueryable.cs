using System.Linq.Expressions;

namespace DapperToolkit.Core.Interfaces;

public interface IIncludableQueryable<T, TProperty>
{
  Task<IEnumerable<T>> ToListAsync();
  IIncludableQueryable<T, TNextProperty> ThenInclude<TNextProperty>(Expression<Func<TProperty, TNextProperty>> navigationPropertyPath);
  IIncludableQueryable<T, TNextProperty> ThenInclude<TNextProperty>(Expression<Func<TProperty, IEnumerable<TNextProperty>>> navigationPropertyPath);
}
