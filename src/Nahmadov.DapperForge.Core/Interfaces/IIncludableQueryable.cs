namespace Nahmadov.DapperForge.Core.Interfaces;

/// <summary>
/// Marker interface to enable Include/ThenInclude chaining similar to EF Core.
/// </summary>
public interface IIncludableQueryable<TEntity, out TProperty> : IDapperQueryable<TEntity>
    where TEntity : class
{
}