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

    private class DummyProvider : DapperToolkit.Core.Interfaces.IDapperConnectionProvider
    {
        public System.Data.IDbConnection CreateConnection() => throw new NotImplementedException();
    }
}