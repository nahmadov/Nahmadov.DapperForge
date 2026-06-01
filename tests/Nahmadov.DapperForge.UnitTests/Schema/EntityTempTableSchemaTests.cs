using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Builders;
using Nahmadov.DapperForge.Core.Modeling.Mapping;
using Nahmadov.DapperForge.Core.Modeling.Schema;
using Nahmadov.DapperForge.Core.Schema;
using Nahmadov.DapperForge.SqlServer;
using Nahmadov.DapperForge.Sqlite;

using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Schema;

public class EntityTempTableSchemaTests
{
    private static EntityMapping Mapping(ISqlDialect dialect)
    {
        var mb = new DapperModelBuilder(dialect);
        mb.Entity<History>(b =>
        {
            b.HasKey(h => h.Id);
            b.Property(h => h.Name).HasColumnName("FullName").HasMaxLength(100).IsRequired();
            b.Property(h => h.Amount).HasColumnType(SqlColumnType.Decimal, 18, 4);
        });
        return mb.Build()[typeof(History)];
    }

    private static string BuildSql(ISqlDialect dialect, IReadOnlyList<string>? subset = null)
    {
        var builder = new TempTableBuilder(dialect, "TmpHistory");
        EntityTempTableSchema.Populate(builder, Mapping(dialect), subset);
        return builder.BuildCreateTableSql();
    }

    [Fact]
    public void SqlServer_MirrorsColumnNamesAndTypes_ExcludingIdentity()
    {
        var sql = BuildSql(SqlServerDialect.Instance);

        Assert.Equal(
            "CREATE TABLE #TmpHistory (" +
            "[MvsId] int NOT NULL, " +
            "[FullName] nvarchar(100) NOT NULL, " +   // HasColumnName + required + max length
            "[Note] nvarchar(max) NULL, " +           // optional string
            "[ParentId] int NULL, " +                 // int?
            "[Amount] decimal(18,4) NOT NULL)",       // HasColumnType
            sql);

        Assert.DoesNotContain("[Id]", sql); // identity excluded by default
    }

    [Fact]
    public void Sqlite_MirrorsColumns_WithAffinityTypes()
    {
        var sql = BuildSql(SqliteDialect.Instance);

        Assert.Equal(
            "CREATE TEMP TABLE \"TmpHistory\" (" +
            "\"MvsId\" INTEGER NOT NULL, " +
            "\"FullName\" TEXT NOT NULL, " +
            "\"Note\" TEXT NULL, " +
            "\"ParentId\" INTEGER NULL, " +
            "\"Amount\" NUMERIC NOT NULL)",
            sql);
    }

    [Fact]
    public void Subset_EmitsOnlyChosenColumns_InOrder_IncludingIdentityWhenExplicit()
    {
        var sql = BuildSql(SqlServerDialect.Instance, ["Name", "Id"]);

        Assert.Equal(
            "CREATE TABLE #TmpHistory (" +
            "[FullName] nvarchar(100) NOT NULL, " +
            "[Id] int NOT NULL)",
            sql);
    }

    [Fact]
    public void Subset_UnmappedProperty_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => BuildSql(SqlServerDialect.Instance, ["DoesNotExist"]));
    }

    [Table("Histories", Schema = "dbo")]
    private sealed class History
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int MvsId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Note { get; set; }

        public int? ParentId { get; set; }

        public decimal Amount { get; set; }
    }
}
