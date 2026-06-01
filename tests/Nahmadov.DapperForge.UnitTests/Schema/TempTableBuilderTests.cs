using Nahmadov.DapperForge.Core.Modeling.Schema;
using Nahmadov.DapperForge.Core.Schema;
using Nahmadov.DapperForge.Oracle;
using Nahmadov.DapperForge.SqlServer;
using Nahmadov.DapperForge.Sqlite;

using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Schema;

public class TempTableBuilderTests
{
    // ── SQL Server ────────────────────────────────────────────────────────────

    [Fact]
    public void SqlServer_BuildsCreateTable_WithHashPrefixAndTypedColumns()
    {
        var sql = new TempTableBuilder(SqlServerDialect.Instance, "TmpHistDaily")
            .Column<int>("MVSID")
            .Column<int>("HistColID")
            .Column<DateTime>("HistDate")
            .Column<double>("Value")
            .Column<DateTime>("InsertDate", nullable: true)
            .BuildCreateTableSql();

        Assert.Equal(
            "CREATE TABLE #TmpHistDaily (" +
            "[MVSID] int NOT NULL, " +
            "[HistColID] int NOT NULL, " +
            "[HistDate] datetime2 NOT NULL, " +
            "[Value] float NOT NULL, " +
            "[InsertDate] datetime2 NULL)",
            sql);
    }

    [Fact]
    public void SqlServer_PreservesExistingHashPrefix()
    {
        var sql = new TempTableBuilder(SqlServerDialect.Instance, "#Already")
            .Column<int>("Id")
            .BuildCreateTableSql();

        Assert.StartsWith("CREATE TABLE #Already (", sql);
        Assert.DoesNotContain("##", sql);
    }

    [Fact]
    public void SqlServer_ExplicitColumnType_WithFacets()
    {
        var sql = new TempTableBuilder(SqlServerDialect.Instance, "Tmp")
            .Column("Code", SqlColumnType.NVarChar, new ColumnTypeFacets(Length: 50))
            .Column("Amount", SqlColumnType.Decimal, new ColumnTypeFacets(Precision: 18, Scale: 4, IsNullable: true))
            .BuildCreateTableSql();

        Assert.Contains("[Code] nvarchar(50) NOT NULL", sql);
        Assert.Contains("[Amount] decimal(18,4) NULL", sql);
    }

    // ── SQLite ────────────────────────────────────────────────────────────────

    [Fact]
    public void Sqlite_BuildsCreateTempTable_WithAffinityTypes()
    {
        var sql = new TempTableBuilder(SqliteDialect.Instance, "TmpHistDaily")
            .Column<int>("MVSID")
            .Column<double>("Value")
            .Column<DateTime>("HistDate")
            .Column<string>("Note", nullable: true)
            .BuildCreateTableSql();

        Assert.Equal(
            "CREATE TEMP TABLE \"TmpHistDaily\" (" +
            "\"MVSID\" INTEGER NOT NULL, " +
            "\"Value\" REAL NOT NULL, " +
            "\"HistDate\" TEXT NOT NULL, " +
            "\"Note\" TEXT NULL)",
            sql);
    }

    // ── Nullability ───────────────────────────────────────────────────────────

    [Fact]
    public void Column_NullableClrType_ImpliesNullable()
    {
        var sql = new TempTableBuilder(SqlServerDialect.Instance, "Tmp")
            .Column<int?>("MaybeId")
            .BuildCreateTableSql();

        Assert.Contains("[MaybeId] int NULL", sql);
    }

    [Fact]
    public void Column_NullableFlag_OverridesNonNullableClrType()
    {
        var sql = new TempTableBuilder(SqlServerDialect.Instance, "Tmp")
            .Column<int>("Id", nullable: true)
            .BuildCreateTableSql();

        Assert.Contains("[Id] int NULL", sql);
    }

    // ── Guard rails ───────────────────────────────────────────────────────────

    [Fact]
    public void Build_WithNoColumns_Throws()
    {
        var builder = new TempTableBuilder(SqlServerDialect.Instance, "Tmp");

        Assert.Throws<InvalidOperationException>(() => builder.BuildCreateTableSql());
    }

    [Fact]
    public void Construct_WithUnsupportedDialect_Throws()
    {
        Assert.Throws<NotSupportedException>(
            () => new TempTableBuilder(OracleDialect.Instance, "Tmp"));
    }

    [Fact]
    public void Construct_WithEmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new TempTableBuilder(SqlServerDialect.Instance, "  "));
    }
}
