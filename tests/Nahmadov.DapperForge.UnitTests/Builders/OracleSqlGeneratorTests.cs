using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Nahmadov.DapperForge.Core.Modeling.Builders;
using Nahmadov.DapperForge.Core.Querying.Sql;
using Nahmadov.DapperForge.Core.Modeling.Mapping;
using Nahmadov.DapperForge.Oracle;
using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Builders;

public class OracleSqlGeneratorTests
{
    [Fact]
    public void SelectAndDelete_QuoteIdentifiers_With_DoubleQuotes()
    {
        var mapping = BuildMapping();
        var generator = new SqlGenerator<OracleEntity>(OracleDialect.Instance, mapping);

        Assert.Contains("FROM \"custom\".\"Users\"", generator.SelectAllSql);
        Assert.Contains("\"Id\" = :Id", generator.DeleteByIdSql);
    }

    [Fact]
    public void Insert_Returning_Id_Appends_Returning_Clause()
    {
        var mapping = BuildMapping();
        var generator = new SqlGenerator<OracleEntity>(OracleDialect.Instance, mapping);

        Assert.NotNull(generator.InsertReturningIdSql);
        Assert.Contains("RETURNING \"Id\" INTO :Id", generator.InsertReturningIdSql);
    }

    private static EntityMapping BuildMapping()
    {
        var builder = new DapperModelBuilder(OracleDialect.Instance);
        builder.Entity<OracleEntity>();
        return builder.Build()[typeof(OracleEntity)];
    }

    [Table("Users", Schema = "custom")]
    private class OracleEntity
    {
        [Key]
        public int Id { get; set; }

        [Column("username")]
        public string Name { get; set; } = string.Empty;
    }
}


