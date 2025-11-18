using DapperToolkit.Core.Attributes;
using DapperToolkit.Core.Mapping;

namespace DapperToolkit.CoreTests.Mapping;

public class ColumnAttributeTypeMapperTests
{
    private class TestEntity
    {
        [ColumnName("col_id")]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void Should_Map_Using_ColumnNameAttribute()
    {
        var mapper = new ColumnAttributeTypeMapper<TestEntity>();
        var member = mapper.GetMember("col_id");

        Assert.NotNull(member);
        Assert.Equal("Id", member!.Property?.Name);
    }

    [Fact]
    public void Should_Map_Using_PropertyName_When_Attribute_NotFound()
    {
        var mapper = new ColumnAttributeTypeMapper<TestEntity>();
        var member = mapper.GetMember("Name");

        Assert.NotNull(member);
        Assert.Equal("Name", member!.Property?.Name);
    }

    [Fact]
    public void Should_Return_Null_If_No_Match_And_Fallback_Fails()
    {
        var mapper = new ColumnAttributeTypeMapper<TestEntity>();
        var member = mapper.GetMember("non_existing");

        Assert.Null(member); // fallback type map də tapa bilməyəcək
    }
}