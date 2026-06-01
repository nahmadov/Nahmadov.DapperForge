using System.Data;

using Dapper;

using Nahmadov.DapperForge.Core.Abstractions;

namespace Nahmadov.DapperForge.Core.Mutations.Bulk;

/// <summary>
/// Bulk-copy fallback for dialects without a native bulk-copy primitive (SQLite). Inserts rows with
/// batched, parameterized multi-row <c>INSERT</c> statements inside a single transaction, splitting
/// batches to respect the dialect's per-statement parameter limit.
/// </summary>
internal sealed class BatchedInsertBulkCopyExecutor : IBulkCopyExecutor
{
    /// <summary>SQLite's conservative per-statement bound variable limit.</summary>
    internal const int MaxParametersPerStatement = 999;

    private readonly ISqlDialect _dialect;

    public BatchedInsertBulkCopyExecutor(ISqlDialect dialect)
        => _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));

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

        if (rows.Rows.Count == 0)
            return 0;

        var columns = rows.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
        if (columns.Count == 0)
            return 0;

        var batchSize = CalculateBatchSize(columns.Count, options.BatchSize);
        var quotedDestination = _dialect.QuoteIdentifier(destinationTable);
        var rowList = rows.Rows.Cast<DataRow>().ToList();

        if (connection.State != ConnectionState.Open)
            connection.Open();

        using var transaction = connection.BeginTransaction();
        var total = 0;

        try
        {
            for (var start = 0; start < rowList.Count; start += batchSize)
            {
                var batch = rowList.GetRange(start, Math.Min(batchSize, rowList.Count - start));
                var sql = BuildInsertSql(quotedDestination, columns, batch.Count, _dialect);
                var parameters = BuildParameters(columns, batch);

                total += await connection.ExecuteAsync(
                    new CommandDefinition(sql, parameters, transaction, commandTimeout: options.TimeoutSeconds, cancellationToken: ct))
                    .ConfigureAwait(false);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        return total;
    }

    /// <summary>
    /// Computes the rows-per-statement so that <c>rows * columns ≤ <see cref="MaxParametersPerStatement"/></c>,
    /// honoring an explicit <paramref name="requestedBatchSize"/> when smaller.
    /// </summary>
    internal static int CalculateBatchSize(int columnCount, int requestedBatchSize)
    {
        if (columnCount <= 0)
            return 0;

        var maxRows = Math.Max(1, MaxParametersPerStatement / columnCount);
        return requestedBatchSize <= 0 ? maxRows : Math.Min(requestedBatchSize, maxRows);
    }

    /// <summary>
    /// Builds a multi-row <c>INSERT INTO dest (cols) VALUES (...), (...)</c> statement with uniquely
    /// named parameters per cell (<c>p{row}_{col}</c>).
    /// </summary>
    internal static string BuildInsertSql(
        string quotedDestination,
        IReadOnlyList<string> columns,
        int rowCount,
        ISqlDialect dialect)
    {
        var columnList = string.Join(", ", columns.Select(dialect.QuoteIdentifier));

        var valueRows = new string[rowCount];
        for (var r = 0; r < rowCount; r++)
        {
            var placeholders = new string[columns.Count];
            for (var c = 0; c < columns.Count; c++)
                placeholders[c] = dialect.FormatParameter($"p{r}_{c}");

            valueRows[r] = $"({string.Join(", ", placeholders)})";
        }

        return $"INSERT INTO {quotedDestination} ({columnList}) VALUES {string.Join(", ", valueRows)}";
    }

    private static DynamicParameters BuildParameters(IReadOnlyList<string> columns, IReadOnlyList<DataRow> batch)
    {
        var parameters = new DynamicParameters();
        for (var r = 0; r < batch.Count; r++)
        {
            var row = batch[r];
            for (var c = 0; c < columns.Count; c++)
            {
                var value = row[c];
                parameters.Add($"p{r}_{c}", value is DBNull ? null : value);
            }
        }

        return parameters;
    }
}
