using System.Data;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Schema;
using Nahmadov.DapperForge.SqlServer;
using Nahmadov.DapperForge.Sqlite;

using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Schema;

public class DataTableSchemaReaderTests
{
    private static DataTable BuildMixedTable()
    {
        var table = new DataTable("Source");
        table.Columns.Add(new DataColumn("Id", typeof(int)) { AllowDBNull = false });
        table.Columns.Add(new DataColumn("Ratio", typeof(double)) { AllowDBNull = false });
        table.Columns.Add(new DataColumn("CreatedAt", typeof(DateTime)) { AllowDBNull = false });
        table.Columns.Add(new DataColumn("IsActive", typeof(bool)) { AllowDBNull = false });
        table.Columns.Add(new DataColumn("ParentId", typeof(int)) { AllowDBNull = true });
        table.Columns.Add(new DataColumn("Name", typeof(string)) { MaxLength = 100, AllowDBNull = true });
        return table;
    }

    private static string BuildSql(ISqlDialect dialect, DataTable table)
    {
        var builder = new TempTableBuilder(dialect, "TmpFromDataTable");
        DataTableSchemaReader.Populate(builder, table);
        return builder.BuildCreateTableSql();
    }

    [Fact]
    public void SqlServer_MapsEveryColumnTypeAndNullability()
    {
        var sql = BuildSql(SqlServerDialect.Instance, BuildMixedTable());

        Assert.Equal(
            "CREATE TABLE #TmpFromDataTable (" +
            "[Id] int NOT NULL, " +
            "[Ratio] float NOT NULL, " +
            "[CreatedAt] datetime2 NOT NULL, " +
            "[IsActive] bit NOT NULL, " +
            "[ParentId] int NULL, " +
            "[Name] nvarchar(100) NULL)",
            sql);
    }

    [Fact]
    public void Sqlite_MapsEveryColumnToAffinity()
    {
        var sql = BuildSql(SqliteDialect.Instance, BuildMixedTable());

        Assert.Equal(
            "CREATE TEMP TABLE \"TmpFromDataTable\" (" +
            "\"Id\" INTEGER NOT NULL, " +
            "\"Ratio\" REAL NOT NULL, " +
            "\"CreatedAt\" TEXT NOT NULL, " +
            "\"IsActive\" INTEGER NOT NULL, " +
            "\"ParentId\" INTEGER NULL, " +
            "\"Name\" TEXT NULL)",
            sql);
    }

    [Fact]
    public void StringColumn_WithoutMaxLength_FallsBackToMax()
    {
        var table = new DataTable("Source");
        table.Columns.Add(new DataColumn("Note", typeof(string)) { AllowDBNull = false }); // MaxLength = -1

        var sql = BuildSql(SqlServerDialect.Instance, table);

        Assert.Contains("[Note] nvarchar(max) NOT NULL", sql);
    }

    [Fact]
    public void AllowDBNull_DrivesNullability_OverInferredClrNullability()
    {
        // string is a reference type (inferred nullable) but AllowDBNull=false forces NOT NULL.
        var table = new DataTable("Source");
        table.Columns.Add(new DataColumn("Code", typeof(string)) { MaxLength = 10, AllowDBNull = false });

        var sql = BuildSql(SqlServerDialect.Instance, table);

        Assert.Contains("[Code] nvarchar(10) NOT NULL", sql);
    }

    [Fact]
    public void EmptyDataTable_Throws()
    {
        var builder = new TempTableBuilder(SqlServerDialect.Instance, "Tmp");

        Assert.Throws<ArgumentException>(
            () => DataTableSchemaReader.Populate(builder, new DataTable("Empty")));
    }
}
