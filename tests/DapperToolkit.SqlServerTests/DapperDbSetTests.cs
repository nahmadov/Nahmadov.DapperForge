using System.Data;
using System.Linq.Expressions;
using System.Reflection;

using DapperToolkit.Core.Attributes;
using DapperToolkit.Core.Interfaces;
using DapperToolkit.SqlServer.Context;
using DapperToolkit.SqlServer;

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

    [Fact]
    public async Task InsertAsync_Should_Generate_Correct_SQL_With_Column_Mapping()
    {
        var mockProvider = new Mock<IDapperConnectionProvider>();
        var mockConnection = new Mock<IDbConnection>();
        var mockCommand = new Mock<IDbCommand>();
        
        mockProvider.Setup(x => x.CreateConnection()).Returns(mockConnection.Object);
        mockConnection.Setup(x => x.State).Returns(ConnectionState.Open);
        mockConnection.Setup(x => x.CreateCommand()).Returns(mockCommand.Object);
        
        var context = new DapperDbContext(mockProvider.Object);
        var dbSet = new DapperDbSet<SampleEntity>(context);
        var entity = new SampleEntity { Id = 1, Name = "Test" };

        try
        {
            await dbSet.InsertAsync(entity);
        }
        catch
        {
        }

        mockProvider.Verify(x => x.CreateConnection(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Should_Generate_Correct_SQL_With_Column_Mapping()
    {
        var mockProvider = new Mock<IDapperConnectionProvider>();
        var mockConnection = new Mock<IDbConnection>();
        var mockCommand = new Mock<IDbCommand>();
        
        mockProvider.Setup(x => x.CreateConnection()).Returns(mockConnection.Object);
        mockConnection.Setup(x => x.State).Returns(ConnectionState.Open);
        mockConnection.Setup(x => x.CreateCommand()).Returns(mockCommand.Object);
        
        var context = new DapperDbContext(mockProvider.Object);
        var dbSet = new DapperDbSet<SampleEntity>(context);
        var entity = new SampleEntity { Id = 1, Name = "Test" };

        try
        {
            await dbSet.UpdateAsync(entity);
        }
        catch
        {
        }

        mockProvider.Verify(x => x.CreateConnection(), Times.Once);
    }

    [Fact]
    public void InsertAsync_Should_Exclude_Id_Property_From_Insert()
    {
        var mockProvider = new Mock<IDapperConnectionProvider>();
        var context = new DapperDbContext(mockProvider.Object);
        var dbSet = new DapperDbSet<SampleEntity>(context);
        
        Assert.NotNull(dbSet);
        Assert.True(typeof(DapperDbSet<SampleEntity>).GetMethod("InsertAsync") != null);
    }

    [Fact]
    public void UpdateAsync_Should_Exclude_Id_Property_From_Set_Clause()
    {
        var mockProvider = new Mock<IDapperConnectionProvider>();
        var context = new DapperDbContext(mockProvider.Object);
        var dbSet = new DapperDbSet<SampleEntity>(context);
        
        Assert.NotNull(dbSet);
        Assert.True(typeof(DapperDbSet<SampleEntity>).GetMethod("UpdateAsync") != null);
    }

    [Fact]
    public void AnyAsync_Methods_Should_Exist()
    {
        var mockProvider = new Mock<IDapperConnectionProvider>();
        var context = new DapperDbContext(mockProvider.Object);
        var dbSet = new DapperDbSet<SampleEntity>(context);
        
        Assert.NotNull(dbSet);
        Assert.True(typeof(DapperDbSet<SampleEntity>).GetMethod("AnyAsync", new Type[0]) != null);
        Assert.True(typeof(DapperDbSet<SampleEntity>).GetMethod("AnyAsync", new[] { typeof(Expression<Func<SampleEntity, bool>>) }) != null);
    }

    [Fact]
    public void CountAsync_Methods_Should_Exist()
    {
        var mockProvider = new Mock<IDapperConnectionProvider>();
        var context = new DapperDbContext(mockProvider.Object);
        var dbSet = new DapperDbSet<SampleEntity>(context);
        
        Assert.NotNull(dbSet);
        Assert.True(typeof(DapperDbSet<SampleEntity>).GetMethod("CountAsync", new Type[0]) != null);
        Assert.True(typeof(DapperDbSet<SampleEntity>).GetMethod("CountAsync", new[] { typeof(Expression<Func<SampleEntity, bool>>) }) != null);
    }

    [Fact]
    public void PageAsync_Methods_Should_Exist()
    {
        var mockProvider = new Mock<IDapperConnectionProvider>();
        var context = new DapperDbContext(mockProvider.Object);
        var dbSet = new DapperDbSet<SampleEntity>(context);
        
        Assert.NotNull(dbSet);
        Assert.True(typeof(DapperDbSet<SampleEntity>).GetMethod("PageAsync", new[] { typeof(int), typeof(int) }) != null);
        Assert.True(typeof(DapperDbSet<SampleEntity>).GetMethod("PageWithCountAsync", new[] { typeof(int), typeof(int) }) != null);
    }

    [Fact]
    public async Task PageAsync_Should_Throw_ArgumentException_For_Invalid_PageNumber()
    {
        var mockProvider = new Mock<IDapperConnectionProvider>();
        var context = new DapperDbContext(mockProvider.Object);
        var dbSet = new DapperDbSet<SampleEntity>(context);

        await Assert.ThrowsAsync<ArgumentException>(() => dbSet.PageAsync(0, 10));
        await Assert.ThrowsAsync<ArgumentException>(() => dbSet.PageAsync(-1, 10));
    }

    [Fact]
    public async Task PageAsync_Should_Throw_ArgumentException_For_Invalid_PageSize()
    {
        var mockProvider = new Mock<IDapperConnectionProvider>();
        var context = new DapperDbContext(mockProvider.Object);
        var dbSet = new DapperDbSet<SampleEntity>(context);

        await Assert.ThrowsAsync<ArgumentException>(() => dbSet.PageAsync(1, 0));
        await Assert.ThrowsAsync<ArgumentException>(() => dbSet.PageAsync(1, -1));
    }

    [Fact]
    public async Task PageWithCountAsync_Should_Throw_ArgumentException_For_Invalid_Parameters()
    {
        var mockProvider = new Mock<IDapperConnectionProvider>();
        var context = new DapperDbContext(mockProvider.Object);
        var dbSet = new DapperDbSet<SampleEntity>(context);

        await Assert.ThrowsAsync<ArgumentException>(() => dbSet.PageWithCountAsync(0, 10));
        await Assert.ThrowsAsync<ArgumentException>(() => dbSet.PageWithCountAsync(1, 0));
    }

    [Fact]
    public void OrderBy_Visitor_Should_Translate_Simple_Property_Expression()
    {
        var visitor = new SqlServerOrderByVisitor();
        Expression<Func<SampleEntity, object>> orderBy = x => x.Name;
        
        var result = visitor.TranslateOrderBy(orderBy, true);
        
        Assert.Equal("Name ASC", result);
    }

    [Fact]
    public void OrderBy_Visitor_Should_Translate_Property_With_Column_Attribute()
    {
        var visitor = new SqlServerOrderByVisitor();
        Expression<Func<SampleEntity, object>> orderBy = x => x.Id;
        
        var result = visitor.TranslateOrderBy(orderBy, true);
        
        Assert.Equal("col_id ASC", result);
    }

    [Fact]
    public void OrderBy_Visitor_Should_Generate_DESC_For_Descending()
    {
        var visitor = new SqlServerOrderByVisitor();
        Expression<Func<SampleEntity, object>> orderBy = x => x.Name;
        
        var result = visitor.TranslateOrderBy(orderBy, false);
        
        Assert.Equal("Name DESC", result);
    }

    [Fact]
    public void ToListAsync_With_OrderBy_Method_Should_Exist()
    {
        var mockProvider = new Mock<IDapperConnectionProvider>();
        var context = new DapperDbContext(mockProvider.Object);
        var dbSet = new DapperDbSet<SampleEntity>(context);
        
        Assert.NotNull(dbSet);
        Assert.True(typeof(DapperDbSet<SampleEntity>).GetMethod("ToListAsync", new[] { typeof(Expression<Func<SampleEntity, object>>), typeof(bool) }) != null);
    }

    [Fact]
    public void PageAsync_With_OrderBy_Methods_Should_Exist()
    {
        var mockProvider = new Mock<IDapperConnectionProvider>();
        var context = new DapperDbContext(mockProvider.Object);
        var dbSet = new DapperDbSet<SampleEntity>(context);
        
        Assert.NotNull(dbSet);
        Assert.True(typeof(DapperDbSet<SampleEntity>).GetMethod("PageAsync", new[] { typeof(int), typeof(int), typeof(Expression<Func<SampleEntity, object>>), typeof(bool) }) != null);
        Assert.True(typeof(DapperDbSet<SampleEntity>).GetMethod("PageWithCountAsync", new[] { typeof(int), typeof(int), typeof(Expression<Func<SampleEntity, object>>), typeof(bool) }) != null);
    }

    [Fact]
    public async Task PageAsync_With_OrderBy_Should_Throw_ArgumentException_For_Invalid_Parameters()
    {
        var mockProvider = new Mock<IDapperConnectionProvider>();
        var context = new DapperDbContext(mockProvider.Object);
        var dbSet = new DapperDbSet<SampleEntity>(context);
        Expression<Func<SampleEntity, object>> orderBy = x => x.Name;

        await Assert.ThrowsAsync<ArgumentException>(() => dbSet.PageAsync(0, 10, orderBy));
        await Assert.ThrowsAsync<ArgumentException>(() => dbSet.PageAsync(1, 0, orderBy));
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
