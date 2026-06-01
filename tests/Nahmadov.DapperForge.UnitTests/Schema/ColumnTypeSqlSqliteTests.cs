using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Schema;
using Nahmadov.DapperForge.Sqlite;

using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Schema;

public class ColumnTypeSqliteTests
{
    private static readonly ISqlDialect Dialect = SqliteDialect.Instance;

    [Theory]
    [InlineData(SqlColumnType.Boolean, "INTEGER")]
    [InlineData(SqlColumnType.TinyInt, "INTEGER")]
    [InlineData(SqlColumnType.SmallInt, "INTEGER")]
    [InlineData(SqlColumnType.Int, "INTEGER")]
    [InlineData(SqlColumnType.BigInt, "INTEGER")]
    [InlineData(SqlColumnType.Float, "REAL")]
    [InlineData(SqlColumnType.Real, "REAL")]
    [InlineData(SqlColumnType.Decimal, "NUMERIC")]
    [InlineData(SqlColumnType.Money, "NUMERIC")]
    [InlineData(SqlColumnType.Date, "TEXT")]
    [InlineData(SqlColumnType.Time, "TEXT")]
    [InlineData(SqlColumnType.DateTime, "TEXT")]
    [InlineData(SqlColumnType.DateTime2, "TEXT")]
    [InlineData(SqlColumnType.DateTimeOffset, "TEXT")]
    [InlineData(SqlColumnType.Char, "TEXT")]
    [InlineData(SqlColumnType.VarChar, "TEXT")]
    [InlineData(SqlColumnType.NChar, "TEXT")]
    [InlineData(SqlColumnType.NVarChar, "TEXT")]
    [InlineData(SqlColumnType.Text, "TEXT")]
    [InlineData(SqlColumnType.Guid, "TEXT")]
    [InlineData(SqlColumnType.Binary, "BLOB")]
    [InlineData(SqlColumnType.VarBinary, "BLOB")]
    public void GetColumnTypeSql_MapsToAffinity(SqlColumnType type, string expected)
    {
        Assert.Equal(expected, Dialect.GetColumnTypeSql(type, default));
    }

    [Fact]
    public void GetColumnTypeSql_IgnoresFacets_ForAffinity()
    {
        // Facets do not change SQLite affinity names.
        var sql = Dialect.GetColumnTypeSql(SqlColumnType.NVarChar, new ColumnTypeFacets(Length: 50));

        Assert.Equal("TEXT", sql);
    }
}
