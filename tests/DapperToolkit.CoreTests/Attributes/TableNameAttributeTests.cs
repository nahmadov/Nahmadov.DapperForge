using System.Reflection;

using DapperToolkit.Core.Attributes;

namespace DapperToolkit.CoreTests.Attributes;

public class TableNameAttributeTests
{
    [TableName("USERS_TABLE")]
    private class SampleEntity { }

    [Fact]
    public void Should_Return_Correct_TableName()
    {
        var attribute = typeof(SampleEntity).GetCustomAttribute<TableNameAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal("USERS_TABLE", attribute!.Name);
    }
}