using DapperToolkit.Core.Interfaces;
using DapperToolkit.SqlServer.Context;

using Moq;

namespace DapperToolkit.SqlServerTests;

public class DapperDbContextTests
{
    private class TestEntity { }

    [Fact]
    public void Set_Should_Return_Valid_DapperDbSet_Instance()
    {
        // Arrange
        var mockProvider = new Mock<IDapperConnectionProvider>();
        var context = new DapperDbContext(mockProvider.Object);

        // Act
        var dbSet = context.Set<TestEntity>();

        // Assert
        Assert.NotNull(dbSet);
        Assert.IsAssignableFrom<IDapperDbSet<TestEntity>>(dbSet);
    }
}