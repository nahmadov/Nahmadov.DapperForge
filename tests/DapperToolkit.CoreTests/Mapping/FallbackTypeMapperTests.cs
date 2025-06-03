using Dapper;

using DapperToolkit.Core.Mapping;

using Moq;

namespace DapperToolkit.CoreTests.Mapping;

public class FallbackTypeMapperTests
{
    [Fact]
    public void Should_Return_First_Valid_MemberMap()
    {
        var mockMap1 = new Mock<SqlMapper.ITypeMap>();
        mockMap1.Setup(m => m.GetMember("col")).Returns((SqlMapper.IMemberMap?)null);

        var mockMap2 = new Mock<SqlMapper.ITypeMap>();
        var fakeMemberMap = new Mock<SqlMapper.IMemberMap>();
        mockMap2.Setup(m => m.GetMember("col")).Returns(fakeMemberMap.Object);

        var fallback = new FallbackTypeMapper([mockMap1.Object, mockMap2.Object]);

        var result = fallback.GetMember("col");

        Assert.NotNull(result);
    }

    [Fact]
    public void Should_Return_Null_When_All_Mappers_Fail()
    {
        var mockMap1 = new Mock<SqlMapper.ITypeMap>();
        var mockMap2 = new Mock<SqlMapper.ITypeMap>();

        mockMap1.Setup(m => m.GetMember("col")).Returns((SqlMapper.IMemberMap?)null);
        mockMap2.Setup(m => m.GetMember("col")).Returns((SqlMapper.IMemberMap?)null);

        var fallback = new FallbackTypeMapper([mockMap1.Object, mockMap2.Object]);

        var result = fallback.GetMember("col");

        Assert.Null(result);
    }
}