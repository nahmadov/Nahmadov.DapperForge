using System.Data;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Mutations.Bulk;
using Nahmadov.DapperForge.SqlServer;
using Nahmadov.DapperForge.UnitTests.Fakes;

using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Bulk;

public class SqlBulkCopyExecutorTests
{
    [Fact]
    public void BuildColumnMappings_MapsEachDataTableColumnByName()
    {
        var table = new DataTable();
        table.Columns.Add(new DataColumn("MVSID", typeof(int)));
        table.Columns.Add(new DataColumn("HistDate", typeof(DateTime)));
        table.Columns.Add(new DataColumn("Value", typeof(double)));

        var mappings = SqlBulkCopyExecutor.BuildColumnMappings(table);

        Assert.Equal(
            new[] { ("MVSID", "MVSID"), ("HistDate", "HistDate"), ("Value", "Value") },
            mappings.ToArray());
    }

    [Fact]
    public void SqlServerDialect_ExposesBulkCopySupport()
    {
        ISqlDialect dialect = SqlServerDialect.Instance;

        Assert.True(dialect.SupportsBulkCopy);
        Assert.IsType<SqlBulkCopyExecutor>(dialect.CreateBulkCopyExecutor());
    }

    [Fact]
    public async Task BulkCopyAsync_NonSqlConnection_Throws()
    {
        var executor = (IBulkCopyExecutor)new SqlBulkCopyExecutor();
        var table = new DataTable();
        table.Columns.Add(new DataColumn("Id", typeof(int)));
        table.Rows.Add(1);

        await Assert.ThrowsAsync<ArgumentException>(
            () => executor.BulkCopyAsync(new FakeDbConnection(), "Dest", table, new BulkCopyOptions()));
    }
}
