using System.Linq.Expressions;

namespace DapperToolkit.Core.Builders;

public static class FilterBuilder<TEntity>
{
    public static FilterExpression<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
    {
        var visitor = new PredicateVisitor();
        var (sql, parameters) = visitor.Translate(predicate);

        return new FilterExpression<TEntity>(sql, parameters);
    }
}