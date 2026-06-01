using System.Data;

using Microsoft.Data.SqlClient;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Mutations.Bulk;

namespace Nahmadov.DapperForge.SqlServer;

/// <summary>
/// SQL Server bulk-copy executor backed by <see cref="SqlBulkCopy"/>.
/// </summary>
internal sealed class SqlBulkCopyExecutor : IBulkCopyExecutor
{
    public async Task<int> BulkCopyAsync(
        IDbConnection connection,
        string destinationTable,
        DataTable rows,
        BulkCopyOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(destinationTable))
            throw new ArgumentException("Destination table name cannot be empty.", nameof(destinationTable));

        if (connection is not SqlConnection sqlConnection)
            throw new ArgumentException(
                $"SQL Server bulk copy requires a {nameof(SqlConnection)} but got '{connection.GetType().Name}'.",
                nameof(connection));

        if (rows.Rows.Count == 0)
            return 0;

        if (sqlConnection.State != ConnectionState.Open)
            await sqlConnection.OpenAsync(ct).ConfigureAwait(false);

        var copyOptions = options.UseTableLock ? SqlBulkCopyOptions.TableLock : SqlBulkCopyOptions.Default;

        using var bulkCopy = new SqlBulkCopy(sqlConnection, copyOptions, externalTransaction: null)
        {
            DestinationTableName = destinationTable,
            BulkCopyTimeout = options.TimeoutSeconds,
            EnableStreaming = options.EnableStreaming
        };

        if (options.BatchSize > 0)
            bulkCopy.BatchSize = options.BatchSize;

        // Map by name so column order in the DataTable need not match the destination.
        foreach (var (source, destination) in BuildColumnMappings(rows))
            bulkCopy.ColumnMappings.Add(source, destination);

        await bulkCopy.WriteToServerAsync(rows, ct).ConfigureAwait(false);

        return rows.Rows.Count;
    }

    /// <summary>
    /// Derives source→destination column mappings from the <see cref="DataTable"/> columns. The
    /// <see cref="DataTable"/> is built from the entity mapping (or supplied directly), so its column
    /// names already match the destination table's columns.
    /// </summary>
    internal static IReadOnlyList<(string Source, string Destination)> BuildColumnMappings(DataTable rows)
        => rows.Columns.Cast<DataColumn>().Select(c => (c.ColumnName, c.ColumnName)).ToList();
}
