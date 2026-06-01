using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;

using Nahmadov.DapperForge.Core.Modeling.Builders;
using Nahmadov.DapperForge.Core.Modeling.Mapping;
using Nahmadov.DapperForge.Core.Mutations.Bulk;
using Nahmadov.DapperForge.SqlServer;

using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Bulk;

public class EntityDataTableFactoryTests
{
    private static EntityMapping Mapping()
    {
        var mb = new DapperModelBuilder(SqlServerDialect.Instance);
        mb.Entity<History>(b =>
        {
            b.HasKey(h => h.Id);
            b.Property(h => h.Name).HasColumnName("FullName");
        });
        return mb.Build()[typeof(History)];
    }

    [Fact]
    public void ToDataTable_ExcludesIdentity_AndUsesResolvedColumnNames()
    {
        var table = EntityDataTableFactory.ToDataTable(Mapping(), Array.Empty<History>());

        var columnNames = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray();

        Assert.Equal(new[] { "MvsId", "FullName", "ParentId", "Amount" }, columnNames);
        Assert.DoesNotContain("Id", columnNames); // identity excluded
    }

    [Fact]
    public void ToDataTable_UnwrapsNullableColumnTypes()
    {
        var table = EntityDataTableFactory.ToDataTable(Mapping(), Array.Empty<History>());

        Assert.Equal(typeof(int), table.Columns["ParentId"]!.DataType); // int? -> int
    }

    [Fact]
    public void ToDataTable_PopulatesRowValues_WithDbNullForNulls()
    {
        var rows = new[]
        {
            new History { Id = 1, MvsId = 10, Name = "a", ParentId = 5, Amount = 1.5m },
            new History { Id = 2, MvsId = 20, Name = "b", ParentId = null, Amount = 2.5m },
        };

        var table = EntityDataTableFactory.ToDataTable(Mapping(), rows);

        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(10, table.Rows[0]["MvsId"]);
        Assert.Equal("a", table.Rows[0]["FullName"]);
        Assert.Equal(5, table.Rows[0]["ParentId"]);
        Assert.Equal(DBNull.Value, table.Rows[1]["ParentId"]); // null -> DBNull
    }

    [Table("Histories")]
    private sealed class History
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int MvsId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public decimal Amount { get; set; }
    }
}
