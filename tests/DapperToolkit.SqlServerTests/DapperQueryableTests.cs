using System.Linq.Expressions;

using DapperToolkit.SqlServer.Context;
using DapperToolkit.SqlServer.Query;

namespace DapperToolkit.SqlServerTests;

public class DapperQueryableTests
{
    private class TestEntity
    {
        public int Id { get; set; }
        public bool IsActive { get; set; }
    }

    [Fact]
    public void Where_Should_Combine_Expressions()
    {
        // Arrange
        var ctx = new DapperDbContext(new DummyProvider());
        var queryable = new DapperQueryable<TestEntity>(ctx);

        Expression<Func<TestEntity, bool>> expr1 = x => x.Id > 5;
        Expression<Func<TestEntity, bool>> expr2 = x => x.IsActive;

        // Act
        var combined = queryable.Where(expr1).Where(expr2);

        // Assert: combine call does not throw
        Assert.NotNull(combined);
    }

    [Fact]
    public void OrderBy_Should_Return_New_Queryable_With_Ordering()
    {
        var ctx = new DapperDbContext(new DummyProvider());
        var queryable = new DapperQueryable<TestEntity>(ctx);

        Expression<Func<TestEntity, object>> orderBy = x => x.Id;

        var ordered = queryable.OrderBy(orderBy);

        Assert.NotNull(ordered);
        Assert.NotSame(queryable, ordered);
    }

    [Fact]
    public void OrderByDescending_Should_Return_New_Queryable_With_Descending_Ordering()
    {
        var ctx = new DapperDbContext(new DummyProvider());
        var queryable = new DapperQueryable<TestEntity>(ctx);

        Expression<Func<TestEntity, object>> orderBy = x => x.Id;

        var ordered = queryable.OrderByDescending(orderBy);

        Assert.NotNull(ordered);
        Assert.NotSame(queryable, ordered);
    }

    [Fact]
    public void OrderBy_Should_Chain_With_Where()
    {
        var ctx = new DapperDbContext(new DummyProvider());
        var queryable = new DapperQueryable<TestEntity>(ctx);

        Expression<Func<TestEntity, bool>> where = x => x.IsActive;
        Expression<Func<TestEntity, object>> orderBy = x => x.Id;

        var chained = queryable.Where(where).OrderBy(orderBy);

        Assert.NotNull(chained);
    }

    [Fact]
    public void Where_Should_Chain_With_OrderBy()
    {
        var ctx = new DapperDbContext(new DummyProvider());
        var queryable = new DapperQueryable<TestEntity>(ctx);

        Expression<Func<TestEntity, bool>> where = x => x.IsActive;
        Expression<Func<TestEntity, object>> orderBy = x => x.Id;

        var chained = queryable.OrderBy(orderBy).Where(where);

        Assert.NotNull(chained);
    }

    private class DummyProvider : DapperToolkit.Core.Interfaces.IDapperConnectionProvider
    {
        public System.Data.IDbConnection CreateConnection() => throw new NotImplementedException();
    }
}