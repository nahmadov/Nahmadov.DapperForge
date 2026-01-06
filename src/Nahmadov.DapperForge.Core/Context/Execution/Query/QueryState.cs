using System.Linq.Expressions;

namespace Nahmadov.DapperForge.Core.Context.Execution.Query;

/// <summary>
/// Captures the shape of a query being built for a given entity type.
/// </summary>
/// <typeparam name="TEntity">Entity type.</typeparam>
internal sealed class QueryState<TEntity> where TEntity : class
{
    public Expression<Func<TEntity, bool>>? Predicate { get; set; }
    public bool IgnoreCase { get; set; }
    public List<(Expression<Func<TEntity, object?>> keySelector, bool isDescending)> OrderBy { get; } = [];
    public int Skip { get; set; }
    public int Take { get; set; } = int.MaxValue;
    public bool Distinct { get; set; }

    public bool NeedsPaging => Skip > 0 || Take < int.MaxValue;
}
