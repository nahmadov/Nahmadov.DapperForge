using System.Reflection;

using DapperToolkit.Core.Attributes;

namespace DapperToolkit.CoreTests.Attributes;

public class ForeignKeyNameAttributeTests
{
    private class SampleEntity
    {
        [ForeignKeyName("FK_User_Order")]
        public int OrderId { get; set; }
    }

    [Fact]
    public void Should_Return_Correct_ForeignKeyName()
    {
        var property = typeof(SampleEntity).GetProperty(nameof(SampleEntity.OrderId))!;
        var attribute = property.GetCustomAttribute<ForeignKeyNameAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal("FK_User_Order", attribute!.Name);
    }
}