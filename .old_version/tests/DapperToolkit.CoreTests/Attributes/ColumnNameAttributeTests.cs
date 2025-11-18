using System.Reflection;

using DapperToolkit.Core.Attributes;

namespace DapperToolkit.CoreTests.Attributes;

public class ColumnNameAttributeTests
{
    private class SampleEntity
    {
        [ColumnName("COLUMN_ABC")]
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void ColumnNameAttribute_Should_Return_Correct_Name()
    {
        // Arrange
        var property = typeof(SampleEntity).GetProperty(nameof(SampleEntity.Name))!;

        // Act
        var attribute = property.GetCustomAttribute<ColumnNameAttribute>();

        // Assert
        Assert.NotNull(attribute);
        Assert.Equal("COLUMN_ABC", attribute!.Name);
    }

    [Fact]
    public void ColumnNameAttribute_Should_Have_AttributeUsage_PropertyOnly()
    {
        // Act
        var usage = typeof(ColumnNameAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        // Assert
        Assert.NotNull(usage);
        Assert.True(usage!.ValidOn.HasFlag(AttributeTargets.Property));
    }
}