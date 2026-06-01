using System.Data;

using Nahmadov.DapperForge.Core.Mutations.Bulk;
using Nahmadov.DapperForge.Sqlite;
using Nahmadov.DapperForge.UnitTests.Fakes;

using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Bulk;

public class BatchedInsertBulkCopyTests
{
    // ── Batch size calculation ────────────────────────────────────────────────

    [Theory]
    [InlineData(5, 0, 199)]     // 999 / 5 = 199 (auto)
    [InlineData(5, 50, 50)]     // requested within limit
    [InlineData(5, 1000, 199)]  // requested exceeds limit -> capped
    [InlineData(1000, 0, 1)]    // more columns than the limit -> at least 1 row
    public void CalculateBatchSize_RespectsParameterLimit(int columns, int requested, int expected)
    {
        Assert.Equal(expected, BatchedInsertBulkCopyExecutor.CalculateBatchSize(columns, requested));
    }

    // ── SQL generation ────────────────────────────────────────────────────────

    [Fact]
    public void BuildInsertSql_GeneratesMultiRowValues_WithUniqueParameters()
    {
        var sql = BatchedInsertBulkCopyExecutor.BuildInsertSql(
            "\"Dest\"", ["a", "b"], rowCount: 2, SqliteDialect.Instance);

        Assert.Equal(
            "INSERT INTO \"Dest\" (\"a\", \"b\") VALUES (@p0_0, @p0_1), (@p1_0, @p1_1)",
            sql);
    }

    // ── End-to-end batching (no live DB) ──────────────────────────────────────

    [Fact]
    public async Task BulkCopyAsync_ChunksRows_RespectingParameterLimit()
    {
        var executor = new BatchedInsertBulkCopyExecutor(SqliteDialect.Instance);
        var table = BuildTable(columns: 5, rows: 250);
        var connection = new RecordingDbConnection();

        var copied = await executor.BulkCopyAsync(connection, "Dest", table, new BulkCopyOptions());

        // 5 columns -> batch size 199 -> 250 rows split into 199 + 51.
        Assert.Equal(250, copied);
        Assert.Equal(2, connection.ExecutedCommands.Count);
        Assert.Equal(199, connection.ExecutedCommands[0].RowCount);
        Assert.Equal(51, connection.ExecutedCommands[1].RowCount);

        // Every statement stays within the SQLite parameter cap.
        Assert.All(connection.ExecutedCommands,
            c => Assert.True(c.ParameterCount <= BatchedInsertBulkCopyExecutor.MaxParametersPerStatement));
        Assert.Equal(995, connection.ExecutedCommands[0].ParameterCount); // 199 * 5
    }

    [Fact]
    public async Task BulkCopyAsync_RespectsExplicitBatchSize()
    {
        var executor = new BatchedInsertBulkCopyExecutor(SqliteDialect.Instance);
        var table = BuildTable(columns: 3, rows: 100);
        var connection = new RecordingDbConnection();

        var copied = await executor.BulkCopyAsync(
            connection, "Dest", table, new BulkCopyOptions { BatchSize = 40 });

        Assert.Equal(100, copied);
        Assert.Equal(3, connection.ExecutedCommands.Count); // 40 + 40 + 20
        Assert.Equal(40, connection.ExecutedCommands[0].RowCount);
        Assert.Equal(20, connection.ExecutedCommands[2].RowCount);
    }

    [Fact]
    public async Task BulkCopyAsync_EmptyTable_CopiesNothing()
    {
        var executor = new BatchedInsertBulkCopyExecutor(SqliteDialect.Instance);
        var connection = new RecordingDbConnection();

        var copied = await executor.BulkCopyAsync(connection, "Dest", BuildTable(3, 0), new BulkCopyOptions());

        Assert.Equal(0, copied);
        Assert.Empty(connection.ExecutedCommands);
    }

    private static DataTable BuildTable(int columns, int rows)
    {
        var table = new DataTable();
        for (var c = 0; c < columns; c++)
            table.Columns.Add(new DataColumn($"Col{c}", typeof(int)));

        for (var r = 0; r < rows; r++)
        {
            var values = new object[columns];
            for (var c = 0; c < columns; c++)
                values[c] = r * 100 + c;
            table.Rows.Add(values);
        }

        return table;
    }
}
