using Nahmadov.DapperForge.Core.Modeling.Schema;

using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Schema;

public class SqlColumnTypeInferenceTests
{
    [Theory]
    [InlineData(typeof(bool), SqlColumnType.Boolean)]
    [InlineData(typeof(byte), SqlColumnType.TinyInt)]
    [InlineData(typeof(short), SqlColumnType.SmallInt)]
    [InlineData(typeof(int), SqlColumnType.Int)]
    [InlineData(typeof(long), SqlColumnType.BigInt)]
    [InlineData(typeof(decimal), SqlColumnType.Decimal)]
    [InlineData(typeof(double), SqlColumnType.Float)]
    [InlineData(typeof(float), SqlColumnType.Real)]
    [InlineData(typeof(DateTime), SqlColumnType.DateTime2)]
    [InlineData(typeof(DateOnly), SqlColumnType.Date)]
    [InlineData(typeof(TimeOnly), SqlColumnType.Time)]
    [InlineData(typeof(DateTimeOffset), SqlColumnType.DateTimeOffset)]
    [InlineData(typeof(Guid), SqlColumnType.Guid)]
    [InlineData(typeof(byte[]), SqlColumnType.VarBinary)]
    public void Infer_MapsClrTypeToExpectedColumnType(Type clrType, SqlColumnType expected)
    {
        var (type, _) = SqlColumnTypeInference.Infer(clrType);

        Assert.Equal(expected, type);
    }

    [Fact]
    public void Infer_String_WithoutMaxLength_IsNVarCharMax()
    {
        var (type, facets) = SqlColumnTypeInference.Infer(typeof(string));

        Assert.Equal(SqlColumnType.NVarChar, type);
        Assert.True(facets.IsMaxLength);
    }

    [Fact]
    public void Infer_String_WithMaxLength_UsesLength()
    {
        var (type, facets) = SqlColumnTypeInference.Infer(typeof(string), maxLength: 100);

        Assert.Equal(SqlColumnType.NVarChar, type);
        Assert.Equal(100, facets.Length);
        Assert.False(facets.IsMaxLength);
    }

    [Fact]
    public void Infer_Decimal_HasDefaultPrecisionAndScale()
    {
        var (type, facets) = SqlColumnTypeInference.Infer(typeof(decimal));

        Assert.Equal(SqlColumnType.Decimal, type);
        Assert.Equal(18, facets.Precision);
        Assert.Equal(2, facets.Scale);
    }

    [Fact]
    public void Infer_NonNullableValueType_IsNotNullable()
    {
        var (_, facets) = SqlColumnTypeInference.Infer(typeof(int));

        Assert.False(facets.IsNullable);
    }

    [Fact]
    public void Infer_NullableValueType_UnwrapsAndSetsNullable()
    {
        var (type, facets) = SqlColumnTypeInference.Infer(typeof(int?));

        Assert.Equal(SqlColumnType.Int, type);
        Assert.True(facets.IsNullable);
    }

    [Fact]
    public void Infer_NullableDateTime_UnwrapsToDateTime2()
    {
        var (type, facets) = SqlColumnTypeInference.Infer(typeof(DateTime?));

        Assert.Equal(SqlColumnType.DateTime2, type);
        Assert.True(facets.IsNullable);
    }

    [Fact]
    public void Infer_ReferenceType_IsNullable()
    {
        var (_, facets) = SqlColumnTypeInference.Infer(typeof(string));

        Assert.True(facets.IsNullable);
    }

    [Fact]
    public void Infer_Enum_UsesUnderlyingType()
    {
        var (type, _) = SqlColumnTypeInference.Infer(typeof(ByteEnum));

        Assert.Equal(SqlColumnType.TinyInt, type);
    }

    [Fact]
    public void Infer_NullableEnum_UnwrapsAndSetsNullable()
    {
        var (type, facets) = SqlColumnTypeInference.Infer(typeof(IntEnum?));

        Assert.Equal(SqlColumnType.Int, type);
        Assert.True(facets.IsNullable);
    }

    [Fact]
    public void Infer_UnsupportedType_Throws()
    {
        Assert.Throws<NotSupportedException>(() => SqlColumnTypeInference.Infer(typeof(object)));
    }

    private enum ByteEnum : byte { A, B }

    private enum IntEnum { A, B }
}
