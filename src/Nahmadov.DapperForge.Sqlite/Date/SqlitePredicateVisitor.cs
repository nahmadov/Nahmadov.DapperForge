using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;
using Nahmadov.DapperForge.Core.Querying.Predicates;

namespace Nahmadov.DapperForge.Sqlite.Date;

/// <summary>
/// SQLite-specific typed predicate visitor that wires <see cref="SqlitePredicateTranslator"/>
/// into the standard <see cref="PredicateVisitor{TEntity}"/> pipeline.
/// </summary>
/// <remarks>
/// Returned by <see cref="SqliteDialect.CreatePredicateVisitor{TEntity}"/> so every
/// <c>WhereAsync</c>, <c>Where</c>, <c>FirstOrDefaultAsync</c>, <c>CountAsync</c>, and
/// Include-filter call automatically supports <see cref="SqliteDate"/> expression markers
/// without any change to the calling code.
/// </remarks>
/// <typeparam name="TEntity">The entity type being queried.</typeparam>
public sealed class SqlitePredicateVisitor<TEntity> : PredicateVisitor<TEntity>
    where TEntity : class
{
    /// <summary>
    /// Initializes the visitor with a <see cref="SqlitePredicateTranslator"/> for the given mapping.
    /// </summary>
    public SqlitePredicateVisitor(EntityMapping mapping, ISqlDialect dialect)
        : base(new SqlitePredicateTranslator(mapping, dialect)) { }
}
