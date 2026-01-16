using System.Linq.Expressions;

namespace Nahmadov.DapperForge.Core.Abstractions;
/// <summary>
/// Represents a queryable interface for building queries with fluent API.
/// </summary>
public interface IDapperQueryable<TEntity> where TEntity : class
{
    /// <summary>
    /// Filters results with the specified predicate.
    /// </summary>
    IDapperQueryable<TEntity> Where(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false);

    /// <summary>
    /// Orders results by the specified property in ascending order.
    /// </summary>
    IDapperQueryable<TEntity> OrderBy(Expression<Func<TEntity, object?>> keySelector);

    /// <summary>
    /// Orders results by the specified property in descending order.
    /// </summary>
    IDapperQueryable<TEntity> OrderByDescending(Expression<Func<TEntity, object?>> keySelector);

    /// <summary>
    /// Applies an additional ascending sort order to the query.
    /// </summary>
    IDapperQueryable<TEntity> ThenBy(Expression<Func<TEntity, object?>> keySelector);

    /// <summary>
    /// Applies an additional descending sort order to the query.
    /// </summary>
    IDapperQueryable<TEntity> ThenByDescending(Expression<Func<TEntity, object?>> keySelector);

    /// <summary>
    /// Ensures only distinct rows are returned.
    /// </summary>
    IDapperQueryable<TEntity> Distinct();

    /// <summary>
    /// Skips the specified number of rows.
    /// </summary>
    IDapperQueryable<TEntity> Skip(int count);

    /// <summary>
    /// Takes the specified number of rows.
    /// </summary>
    IDapperQueryable<TEntity> Take(int count);

    /// <summary>
    /// Configures the query to use split queries for Include operations.
    /// </summary>
    IDapperQueryable<TEntity> AsSplitQuery();

    /// <summary>
    /// Configures the query to use a single query with JOINs for Include operations.
    /// </summary>
    IDapperQueryable<TEntity> AsSingleQuery();

    /// <summary>
    /// Disables identity resolution for this query.
    /// </summary>
    IDapperQueryable<TEntity> AsNoIdentityResolution();

    /// <summary>
    /// Specifies a navigation property to include in the query results.
    /// </summary>
    IIncludableQueryable<TEntity, TProperty> Include<TProperty>(Expression<Func<TEntity, TProperty>> navigationSelector);

    /// <summary>
    /// Specifies an additional navigation property to include from the previous Include/ThenInclude.
    /// </summary>
    IIncludableQueryable<TEntity, TNextProperty> ThenInclude<TPrevious, TNextProperty>(Expression<Func<TPrevious, TNextProperty>> navigationSelector)
        where TPrevious : class;

    /// <summary>
    /// Executes the query and returns all matching results.
    /// </summary>
    Task<IEnumerable<TEntity>> ToListAsync();

    /// <summary>
    /// Executes the query and returns the first result.
    /// </summary>
    Task<TEntity> FirstAsync();

    /// <summary>
    /// Executes the query and returns the first result or null.
    /// </summary>
    Task<TEntity?> FirstOrDefaultAsync();

    /// <summary>
    /// Executes the query and returns the only result.
    /// </summary>
    Task<TEntity> SingleAsync();

    /// <summary>
    /// Executes the query and returns the only result or null.
    /// </summary>
    Task<TEntity?> SingleOrDefaultAsync();

    /// <summary>
    /// Executes the query and returns the last result.
    /// </summary>
    Task<TEntity> LastAsync();

    /// <summary>
    /// Executes the query and returns the last result or null.
    /// </summary>
    Task<TEntity?> LastOrDefaultAsync();

    /// <summary>
    /// Determines whether the query returns any results.
    /// </summary>
    Task<bool> AnyAsync();

    /// <summary>
    /// Executes the query and returns the count of matching results.
    /// </summary>
    Task<long> CountAsync();
}

