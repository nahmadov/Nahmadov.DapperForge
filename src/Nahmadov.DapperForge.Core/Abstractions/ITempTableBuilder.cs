using System.Data;

using Nahmadov.DapperForge.Core.Context.Connection;
using Nahmadov.DapperForge.Core.Modeling.Schema;

namespace Nahmadov.DapperForge.Core.Abstractions;

/// <summary>
/// Fluent builder for declaring and creating a session temp table without raw DDL.
/// Obtain one from <see cref="Context.DapperDbContext.TempTable"/>.
/// </summary>
public interface ITempTableBuilder
{
    /// <summary>
    /// Adds a column whose SQL type is inferred from the CLR type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">CLR type to infer the column type from (e.g. <see cref="int"/>, <see cref="string"/>).</typeparam>
    /// <param name="name">Column name.</param>
    /// <param name="nullable">Whether the column accepts NULL. A nullable CLR type (e.g. <c>int?</c>) also implies nullable.</param>
    /// <returns>The current builder for chaining.</returns>
    ITempTableBuilder Column<T>(string name, bool nullable = false);

    /// <summary>
    /// Adds a column with an explicit SQL column type and facets.
    /// </summary>
    /// <param name="name">Column name.</param>
    /// <param name="type">The dialect-agnostic SQL column type.</param>
    /// <param name="facets">Optional length/precision/scale and nullability.</param>
    /// <returns>The current builder for chaining.</returns>
    ITempTableBuilder Column(string name, SqlColumnType type, ColumnTypeFacets facets = default);

    /// <summary>
    /// Builds the <c>CREATE [TEMP] TABLE</c> statement for the configured columns without executing it.
    /// Useful for logging or inspection.
    /// </summary>
    string BuildCreateTableSql();

    /// <summary>
    /// Creates the temp table on the given open connection.
    /// </summary>
    Task CreateAsync(IDbConnection connection, CancellationToken ct = default);

    /// <summary>
    /// Creates the temp table using the connection from the given scope.
    /// </summary>
    Task CreateAsync(IConnectionScope scope, CancellationToken ct = default);
}
