using System.Linq.Expressions;

namespace Nahmadov.DapperForge.Core.Interfaces;

/// <summary>
/// Represents a queryable interface for building DapperForge queries with fluent API.
/// Similar to IQueryable in Entity Framework, allows chaining query operations.
/// </summary>
/// <typeparam name="TEntity">Type of entity being queried.</typeparam>
public interface IDapperQueryable<TEntity> where TEntity : class
{
    /// <summary>
    /// Filters results with the specified predicate.
    /// </summary>
    /// <param name="predicate">Filter condition.</param>
    /// <param name="ignoreCase">Case-insensitive comparison for strings.</param>
    /// <returns>Queryable object for chaining.</returns>
    IDapperQueryable<TEntity> Where(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false);

    /// <summary>
    /// Orders results by the specified property in ascending order.
    /// </summary>
    /// <param name="keySelector">Property to order by.</param>
    /// <returns>Queryable object for chaining.</returns>
    IDapperQueryable<TEntity> OrderBy(Expression<Func<TEntity, object?>> keySelector);

    /// <summary>
    /// Orders results by the specified property in descending order.
    /// </summary>
    /// <param name="keySelector">Property to order by.</param>
    /// <returns>Queryable object for chaining.</returns>
    IDapperQueryable<TEntity> OrderByDescending(Expression<Func<TEntity, object?>> keySelector);

    /// <summary>
    /// Skips the specified number of rows.
    /// </summary>
    /// <param name="count">Number of rows to skip.</param>
    /// <returns>Queryable object for chaining.</returns>
    IDapperQueryable<TEntity> Skip(int count);

    /// <summary>
    /// Takes the specified number of rows.
    /// </summary>
    /// <param name="count">Number of rows to take.</param>
    /// <returns>Queryable object for chaining.</returns>
    IDapperQueryable<TEntity> Take(int count);

    // NEW: EF-like query splitting toggles
    IDapperQueryable<TEntity> AsSplitQuery();
    IDapperQueryable<TEntity> AsSingleQuery();

    // NEW: identity resolution toggle (EF tracking hissinə bənzər)
    IDapperQueryable<TEntity> AsNoIdentityResolution();

    // NEW: Include / ThenInclude (EF-like)
    IIncludableQueryable<TEntity, TRelated?> Include<TRelated>(Expression<Func<TEntity, TRelated?>> navigationSelector)
        where TRelated : class;

    IIncludableQueryable<TEntity, IEnumerable<TRelated>> Include<TRelated>(Expression<Func<TEntity, IEnumerable<TRelated>>> navigationSelector)
        where TRelated : class;

    IIncludableQueryable<TEntity, TNext?> ThenInclude<TPrevious, TNext>(Expression<Func<TPrevious, TNext?>> navigationSelector)
        where TPrevious : class
        where TNext : class;

    IIncludableQueryable<TEntity, IEnumerable<TNext>> ThenInclude<TPrevious, TNext>(Expression<Func<TPrevious, IEnumerable<TNext>>> navigationSelector)
        where TPrevious : class
        where TNext : class;

    /// <summary>
    /// Executes the query and returns all matching results.
    /// </summary>
    Task<IEnumerable<TEntity>> ToListAsync();

    /// <summary>
    /// Executes the query and returns the first result or null.
    /// </summary>
    Task<TEntity?> FirstOrDefaultAsync();

    /// <summary>
    /// Executes the query and returns the only result or null if no results exist.
    /// Throws if more than one result is found.
    /// </summary>
    Task<TEntity?> SingleOrDefaultAsync();

    /// <summary>
    /// Executes the query and returns the count of matching results.
    /// </summary>
    Task<long> CountAsync();
}
