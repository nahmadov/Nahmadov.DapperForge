using System.Linq.Expressions;

namespace Nahmadov.DapperForge.Core.Interfaces;

/// <summary>
/// Represents a queryable interface for building DapperForge queries with fluent API.
/// Similar to IQueryable in Entity Framework, allows chaining query operations.
/// </summary>
/// <typeparam name="TEntity">
/// The entity type being queried. Must be a reference type (class constraint) to support
/// null returns and identity comparison in Include operations.
/// </typeparam>
/// <remarks>
/// <para><b>Performance Characteristics:</b></para>
/// <list type="bullet">
/// <item>All methods are lazily evaluated - query only executes when ToListAsync() or terminal method is called</item>
/// <item>Where predicates are translated to SQL and executed server-side (not client-side filtering)</item>
/// <item>Include operations can use AsSplitQuery() or AsSingleQuery() for performance tuning</item>
/// </list>
/// </remarks>
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
    /// Applies an additional ascending sort order to the query.
    /// Must be called after OrderBy or OrderByDescending.
    /// </summary>
    /// <param name="keySelector">Property to sort by.</param>
    /// <returns>Queryable object for chaining.</returns>
    /// <remarks>
    /// <b>Usage:</b> Chain multiple ThenBy calls for multi-column sorting.
    /// Example: query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.Age)
    /// </remarks>
    IDapperQueryable<TEntity> ThenBy(Expression<Func<TEntity, object?>> keySelector);

    /// <summary>
    /// Applies an additional descending sort order to the query.
    /// Must be called after OrderBy or OrderByDescending.
    /// </summary>
    /// <param name="keySelector">Property to sort by.</param>
    /// <returns>Queryable object for chaining.</returns>
    /// <remarks>
    /// <b>Usage:</b> Chain multiple ThenBy/ThenByDescending calls for multi-column sorting.
    /// Example: query.OrderBy(x => x.Department).ThenByDescending(x => x.Salary).ThenBy(x => x.Name)
    /// </remarks>
    IDapperQueryable<TEntity> ThenByDescending(Expression<Func<TEntity, object?>> keySelector);

    /// <summary>
    /// Ensures only distinct (unique) rows are returned.
    /// Removes duplicate results based on all selected columns.
    /// </summary>
    /// <returns>Queryable object for chaining.</returns>
    /// <remarks>
    /// <b>Performance:</b> Adds DISTINCT keyword to SELECT clause.
    /// Database performs deduplication, which may require sorting/hashing.
    /// For large result sets, consider filtering with WHERE instead if possible.
    /// </remarks>
    IDapperQueryable<TEntity> Distinct();

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

    /// <summary>
    /// Configures the query to use split queries for Include operations.
    /// Each include level executes as a separate SQL query.
    /// </summary>
    /// <returns>Queryable object for chaining.</returns>
    /// <remarks>
    /// <b>Performance:</b> Prevents cartesian explosion with collection navigations.
    /// Recommended for one-to-many relationships or multiple includes.
    /// </remarks>
    IDapperQueryable<TEntity> AsSplitQuery();

    /// <summary>
    /// Configures the query to use a single query with JOINs for Include operations (default).
    /// </summary>
    /// <returns>Queryable object for chaining.</returns>
    /// <remarks>
    /// <b>Performance:</b> Single database round-trip but potential cartesian explosion with collections.
    /// Recommended for single reference navigations or small result sets.
    /// </remarks>
    IDapperQueryable<TEntity> AsSingleQuery();

    /// <summary>
    /// Disables identity resolution for this query.
    /// Each result row creates a new entity instance even if the key already exists in the result set.
    /// </summary>
    /// <returns>Queryable object for chaining.</returns>
    /// <remarks>
    /// <b>Performance:</b> Slightly faster as it skips identity cache lookups.
    /// Use when you don't need object identity preservation in Include operations.
    /// </remarks>
    IDapperQueryable<TEntity> AsNoIdentityResolution();

    /// <summary>
    /// Specifies a navigation property to include in the query results.
    /// Eagerly loads the related entity/entities.
    /// </summary>
    /// <typeparam name="TProperty">
    /// Type of the navigation property. Can be a single reference or a collection.
    /// </typeparam>
    /// <param name="navigationSelector">Expression selecting the navigation property.</param>
    /// <returns>Includable queryable for chaining ThenInclude operations.</returns>
    /// <exception cref="ArgumentNullException">Thrown when navigationSelector is null.</exception>
    /// <exception cref="NotSupportedException">Thrown when navigation property is not configured.</exception>
    /// <remarks>
    /// <b>Performance:</b> Use AsSplitQuery() for collection navigations to avoid cartesian explosion.
    /// </remarks>
    IIncludableQueryable<TEntity, TProperty> Include<TProperty>(Expression<Func<TEntity, TProperty>> navigationSelector);

    /// <summary>
    /// Specifies an additional navigation property to include from the previous Include/ThenInclude.
    /// </summary>
    /// <typeparam name="TPrevious">
    /// Type of the previous navigation property. Must be a reference type (class constraint)
    /// to support null checking and navigation traversal.
    /// </typeparam>
    /// <typeparam name="TNextProperty">
    /// Type of the next navigation property to include.
    /// </typeparam>
    /// <param name="navigationSelector">Expression selecting the next navigation property.</param>
    /// <returns>Includable queryable for chaining additional ThenInclude operations.</returns>
    /// <exception cref="ArgumentNullException">Thrown when navigationSelector is null.</exception>
    /// <exception cref="NotSupportedException">Thrown when navigation property is not configured.</exception>
    /// <remarks>
    /// <b>Performance:</b> Each ThenInclude adds another join or query (depending on AsSplitQuery/AsSingleQuery).
    /// Recommended maximum depth: 3-4 levels.
    /// </remarks>
    IIncludableQueryable<TEntity, TNextProperty> ThenInclude<TPrevious, TNextProperty>(Expression<Func<TPrevious, TNextProperty>> navigationSelector)
        where TPrevious : class;

    /// <summary>
    /// Executes the query and returns all matching results.
    /// </summary>
    Task<IEnumerable<TEntity>> ToListAsync();

    /// <summary>
    /// Executes the query and returns the first result.
    /// Throws if the sequence is empty.
    /// </summary>
    /// <returns>The first matching entity.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when query returns no results (sequence contains no elements).
    /// </exception>
    /// <exception cref="DapperConnectionException">Thrown when database connection fails.</exception>
    /// <exception cref="DapperExecutionException">Thrown when SQL execution fails.</exception>
    Task<TEntity> FirstAsync();

    /// <summary>
    /// Executes the query and returns the first result or null.
    /// </summary>
    /// <returns>The first matching entity or null if no results.</returns>
    /// <exception cref="DapperConnectionException">Thrown when database connection fails.</exception>
    /// <exception cref="DapperExecutionException">Thrown when SQL execution fails.</exception>
    Task<TEntity?> FirstOrDefaultAsync();

    /// <summary>
    /// Executes the query and returns the only result.
    /// Throws if the sequence is empty or contains more than one element.
    /// </summary>
    /// <returns>The single matching entity.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when query returns no results or more than one result.
    /// Use for queries expected to return exactly one result.
    /// </exception>
    /// <exception cref="DapperConnectionException">Thrown when database connection fails.</exception>
    /// <exception cref="DapperExecutionException">Thrown when SQL execution fails.</exception>
    Task<TEntity> SingleAsync();

    /// <summary>
    /// Executes the query and returns the only result or null if no results exist.
    /// Throws if more than one result is found.
    /// </summary>
    /// <returns>The single matching entity or null if no results.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when query returns more than one result.
    /// </exception>
    /// <exception cref="DapperConnectionException">Thrown when database connection fails.</exception>
    /// <exception cref="DapperExecutionException">Thrown when SQL execution fails.</exception>
    Task<TEntity?> SingleOrDefaultAsync();

    /// <summary>
    /// Executes the query and returns the last result.
    /// Throws if the sequence is empty.
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
