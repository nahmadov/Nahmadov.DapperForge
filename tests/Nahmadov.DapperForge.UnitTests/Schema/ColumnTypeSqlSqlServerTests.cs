using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Schema;
using Nahmadov.DapperForge.SqlServer;

using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Schema;

public class ColumnTypeSqlSqlServerTests
{
    private static readonly ISqlDialect Dialect = SqlServerDialect.Instance;

    [Theory]
    [InlineData(SqlColumnType.Boolean, "bit")]
    [InlineData(SqlColumnType.TinyInt, "tinyint")]
    [InlineData(SqlColumnType.SmallInt, "smallint")]
    [InlineData(SqlColumnType.Int, "int")]
    [InlineData(SqlColumnType.BigInt, "bigint")]
    [InlineData(SqlColumnType.Money, "money")]
    [InlineData(SqlColumnType.Float, "float")]
    [InlineData(SqlColumnType.Real, "real")]
    [InlineData(SqlColumnType.Date, "date")]
    [InlineData(SqlColumnType.Time, "time")]
    [InlineData(SqlColumnType.DateTime, "datetime")]
    [InlineData(SqlColumnType.DateTime2, "datetime2")]
    [InlineData(SqlColumnType.DateTimeOffset, "datetimeoffset")]
    [InlineData(SqlColumnType.Text, "nvarchar(max)")]
    [InlineData(SqlColumnType.Guid, "uniqueidentifier")]
    public void GetColumnTypeSql_SimpleTypes(SqlColumnType type, string expected)
    {
        Assert.Equal(expected, Dialect.GetColumnTypeSql(type, default));
    }

    [Fact]
    public void GetColumnTypeSql_NVarChar_WithLength()
    {
        var sql = Dialect.GetColumnTypeSql(SqlColumnType.NVarChar, new ColumnTypeFacets(Length: 50));

        Assert.Equal("nvarchar(50)", sql);
    }

    [Fact]
    public void GetColumnTypeSql_NVarChar_Max()
    {
        var sql = Dialect.GetColumnTypeSql(SqlColumnType.NVarChar, new ColumnTypeFacets(Length: ColumnTypeFacets.Max));

        Assert.Equal("nvarchar(max)", sql);
    }

    [Fact]
    public void GetColumnTypeSql_NVarChar_NoLength_DefaultsToMax()
    {
        var sql = Dialect.GetColumnTypeSql(SqlColumnType.NVarChar, default);

        Assert.Equal("nvarchar(max)", sql);
    }

    [Fact]
    public void GetColumnTypeSql_VarChar_WithLength()
    {
        var sql = Dialect.GetColumnTypeSql(SqlColumnType.VarChar, new ColumnTypeFacets(Length: 200));

        Assert.Equal("varchar(200)", sql);
    }

    [Fact]
    public void GetColumnTypeSql_Decimal_WithPrecisionScale()
    {
        var sql = Dialect.GetColumnTypeSql(SqlColumnType.Decimal, new ColumnTypeFacets(Precision: 18, Scale: 4));

        Assert.Equal("decimal(18,4)", sql);
    }

    [Fact]
    public void GetColumnTypeSql_Decimal_NoFacets_UsesDefaults()
    {
        var sql = Dialect.GetColumnTypeSql(SqlColumnType.Decimal, default);

        Assert.Equal("decimal(18,2)", sql);
    }

    [Fact]
    public void GetColumnTypeSql_VarBinary_Max()
    {
        var sql = Dialect.GetColumnTypeSql(SqlColumnType.VarBinary, default);

        Assert.Equal("varbinary(max)", sql);
    }

    [Fact]
    public void GetColumnTypeSql_NChar_WithLength()
    {
        var sql = Dialect.GetColumnTypeSql(SqlColumnType.NChar, new ColumnTypeFacets(Length: 10));

        Assert.Equal("nchar(10)", sql);
    }
}
