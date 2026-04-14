using System.Linq.Expressions;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;

namespace Nahmadov.DapperForge.Core.Querying.Predicates;

/// <summary>
/// Typed wrapper over <see cref="SqlPredicateTranslator"/> that enforces compile-time safety
/// on the entity type. All translation logic lives in <see cref="SqlPredicateTranslator"/>.
/// </summary>
/// <remarks>
/// Dialect packages can subclass this visitor and supply a custom <see cref="SqlPredicateTranslator"/>
/// (e.g. <c>SqlitePredicateTranslator</c>) via the protected constructor to add dialect-specific
/// expression support without changing the public API.
/// </remarks>
/// <typeparam name="TEntity">The entity type being queried.</typeparam>
public class PredicateVisitor<TEntity> where TEntity : class
{
    private readonly SqlPredicateTranslator _inner;

    /// <summary>
    /// Initializes a new predicate visitor for the given mapping and dialect.
    /// </summary>
    public PredicateVisitor(EntityMapping mapping, ISqlDialect dialect)
        => _inner = new SqlPredicateTranslator(mapping, dialect);

    /// <summary>
    /// Initializes the visitor with a pre-built (possibly dialect-specific) translator.
    /// Use this from subclasses to inject a custom <see cref="SqlPredicateTranslator"/>.
    /// </summary>
    protected PredicateVisitor(SqlPredicateTranslator translator)
        => _inner = translator ?? throw new ArgumentNullException(nameof(translator));

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
