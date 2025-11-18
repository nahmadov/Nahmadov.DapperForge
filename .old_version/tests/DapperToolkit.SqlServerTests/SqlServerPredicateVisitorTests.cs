using System.Linq.Expressions;
using DapperToolkit.SqlServer;

namespace DapperToolkit.SqlServerTests;

public class SqlServerPredicateVisitorTests
{
    private class TestEntity
    {
        public int Id { get; set; }
        public int Age { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    [Fact]
    public void Should_Translate_Equal_Expression()
    {
        Expression<Func<TestEntity, bool>> expr = x => x.Id == 5;

        var visitor = new SqlServerPredicateVisitor();
        var (sql, parameters) = visitor.Translate(expr.Body);

        Assert.Equal("(Id = @p0)", sql);
        Assert.Single(parameters.ParameterNames);
        Assert.Equal(5, parameters.Get<int>("p0"));
    }

    [Fact]
    public void Should_Translate_AndAlso_Expression()
    {
        Expression<Func<TestEntity, bool>> expr = x => x.Id == 5 && x.Age >= 18;

        var visitor = new SqlServerPredicateVisitor();
        var (sql, parameters) = visitor.Translate(expr.Body);

        Assert.Equal("((Id = @p0) AND (Age >= @p1))", sql);
        Assert.Equal(2, parameters.ParameterNames.Count());
        Assert.Equal(5, parameters.Get<int>("p0"));
        Assert.Equal(18, parameters.Get<int>("p1"));
    }

    [Fact]
    public void Should_Throw_For_Unsupported_Operator()
    {
    Expression<Func<TestEntity, bool>> expr = x => !x.IsActive;

        var visitor = new SqlServerPredicateVisitor();

        var ex = Assert.Throws<NotSupportedException>(() => visitor.Translate(expr.Body));
        Assert.Contains("Operator Not", ex.Message);
    }
}