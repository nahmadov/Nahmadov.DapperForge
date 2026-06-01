using System.Data;

using Nahmadov.DapperForge.Core.Mutations.Bulk;

namespace Nahmadov.DapperForge.Core.Abstractions;

/// <summary>
/// Provider-specific bulk-copy strategy. SQL Server implements this with <c>SqlBulkCopy</c>;
/// SQLite uses a batched parameterized-insert fallback. Obtained from
/// <see cref="ISqlDialect.CreateBulkCopyExecutor"/>.
/// </summary>
public interface IBulkCopyExecutor
{
    /// <summary>
    /// Copies every row of <paramref name="rows"/> into <paramref name="destinationTable"/> on the
    /// given open connection. The <see cref="DataTable"/> column names must match the destination
    /// table's column names.
    /// </summary>
    /// <param name="connection">An open connection (temp tables are connection-scoped).</param>
    /// <param name="destinationTable">The destination table name (e.g. a temp table created beforehand).</param>
    /// <param name="rows">The rows to copy; column names map to destination columns.</param>
    /// <param name="options">Bulk-copy options.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The number of rows copied.</returns>
    Task<int> BulkCopyAsync(
        IDbConnection connection,
        string destinationTable,
        DataTable rows,
        BulkCopyOptions options,
        CancellationToken ct = default);
}
