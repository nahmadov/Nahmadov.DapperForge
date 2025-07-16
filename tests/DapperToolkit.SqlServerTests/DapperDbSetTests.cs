using System.Linq.Expressions;
using System.Reflection;

using DapperToolkit.Core.Attributes;
using DapperToolkit.Core.Interfaces;
using DapperToolkit.SqlServer.Context;

using Moq;

namespace DapperToolkit.SqlServerTests;

public class DapperDbSetTests
{
    private class SampleEntity
    {
        [ColumnName("col_id")]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void GetProjection_Should_Build_Correct_Select_Clause()
    {
        var method = typeof(DapperDbSet<SampleEntity>)
            .GetMethod("GetProjection", BindingFlags.NonPublic | BindingFlags.Static)!;

        var result = method.Invoke(null, null) as string;

        Assert.NotNull(result);
        Assert.Contains("col_id AS Id", result);
        Assert.Contains("Name", result);
    }

    [Fact]
    public async Task DeleteAsync_With_Entity_Without_Id_Should_Throw_Exception()
    {
        var mockProvider = new Mock<IDapperConnectionProvider>();
        var context = new DapperDbContext(mockProvider.Object);
        var dbSet = new DapperDbSet<EntityWithoutId>(context);
        var entity = new EntityWithoutId { Name = "Test" };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => dbSet.DeleteAsync(entity));
        Assert.Equal("Entity must have an Id property for deletion.", exception.Message);
    }

    [Fact]
    public async Task DeleteAsync_With_Entity_With_Null_Id_Should_Throw_Exception()
    {
        var mockProvider = new Mock<IDapperConnectionProvider>();
        var context = new DapperDbContext(mockProvider.Object);
        var dbSet = new DapperDbSet<EntityWithNullableId>(context);
        var entity = new EntityWithNullableId { Id = null, Name = "Test" };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => dbSet.DeleteAsync(entity));
        Assert.Equal("Entity Id cannot be null for deletion.", exception.Message);
    }

    private class EntityWithoutId
    {
        public string Name { get; set; } = string.Empty;
    }

    private class EntityWithNullableId
    {
        public int? Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}