using System.Linq.Expressions;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;

namespace Nahmadov.DapperForge.Core.Querying.Predicates;

/// <summary>
/// Typed wrapper over <see cref="SqlPredicateTranslator"/> that enforces compile-time safety
/// on the entity type. All translation logic lives in <see cref="SqlPredicateTranslator"/>.
/// </summary>
/// <typeparam name="TEntity">The entity type being queried.</typeparam>
public sealed class PredicateVisitor<TEntity> where TEntity : class
{
    private readonly SqlPredicateTranslator _inner;

    /// <summary>
    /// Initializes a new predicate visitor for the given mapping and dialect.
    /// </summary>
    public PredicateVisitor(EntityMapping mapping, ISqlDialect dialect)
        => _inner = new SqlPredicateTranslator(mapping, dialect);

    /// <summary>
    /// Translates a boolean predicate expression into a SQL WHERE clause and parameter bag.
    /// </summary>
    /// <param name="predicate">Boolean predicate over <typeparamref name="TEntity"/>.</param>
    /// <param name="ignoreCase">
    /// Optional case-sensitivity override. When <c>null</c>, the dialect default is used.
    /// </param>
    /// <returns>
    /// <c>Sql</c>: WHERE clause text (without the <c>WHERE</c> keyword).
    /// <c>Parameters</c>: dictionary of parameter names to values.
    /// </returns>
    public (string Sql, object Parameters) Translate(
        Expression<Func<TEntity, bool>> predicate,
        bool? ignoreCase = null)
    {
        var (sql, parameters) = _inner.Translate(predicate, ignoreCase);
        return (sql, parameters);
    }
}
