using System.Linq.Expressions;
using Xunit;
using DapperToolkit.Core.Common;
using DapperToolkit.Core.Attributes;

namespace DapperToolkit.CoreTests;

public class NavigationPropertyAnalyzerTests
{
    [TableName("Customers")]
    private class TestCustomer
    {
        [ColumnName("CustomerId")]
        public int Id { get; set; }

        [ColumnName("CustomerName")]
        public string Name { get; set; } = string.Empty;

        [InverseProperty("Customer")]
        public List<TestOrder> Orders { get; set; } = new();
    }

    [TableName("Orders")]
    private class TestOrder
    {
        [ColumnName("OrderId")]
        public int Id { get; set; }

        [ColumnName("CustomerId")]
        [ForeignKey("CustomerId")]
        public int CustomerId { get; set; }

        [ColumnName("OrderDate")]
        public DateTime OrderDate { get; set; }

        public TestCustomer Customer { get; set; } = null!;
    }

    [Fact]
    public void NavigationPropertyAnalyzer_ShouldAnalyzeCustomerOrdersRelationship()
    {
        Expression<Func<TestCustomer, List<TestOrder>>> includeExpression = c => c.Orders;

        var result = NavigationPropertyAnalyzer.AnalyzeIncludeExpression(includeExpression);

        Assert.NotNull(result);
        Assert.Equal("Orders", result.PropertyName);
        Assert.Equal(typeof(TestCustomer), result.SourceType);
        Assert.Equal(typeof(TestOrder), result.TargetType);
        Assert.True(result.ForeignKeyInfo.IsCollection);
        Assert.Equal("CustomerId", result.ForeignKeyInfo.ForeignKeyColumnName);
        Assert.Equal("Customers", result.JoinInfo.SourceTable);
        Assert.Equal("Orders", result.JoinInfo.TargetTable);
        Assert.Equal("CustomerId", result.JoinInfo.SourceForeignKeyColumn);
        Assert.Equal("CustomerId", result.JoinInfo.TargetPrimaryKeyColumn);
        Assert.True(result.JoinInfo.IsOneToMany);
    }

    [Fact]
    public void NavigationPropertyAnalyzer_ShouldAnalyzeOrderCustomerRelationship()
    {
        Expression<Func<TestOrder, TestCustomer>> includeExpression = o => o.Customer;

        var result = NavigationPropertyAnalyzer.AnalyzeIncludeExpression(includeExpression);

        Assert.NotNull(result);
        Assert.Equal("Customer", result.PropertyName);
        Assert.Equal(typeof(TestOrder), result.SourceType);
        Assert.Equal(typeof(TestCustomer), result.TargetType);
        Assert.False(result.ForeignKeyInfo.IsCollection);
        Assert.Equal("CustomerId", result.ForeignKeyInfo.ForeignKeyColumnName);
        Assert.Equal("Orders", result.JoinInfo.SourceTable);
        Assert.Equal("Customers", result.JoinInfo.TargetTable);
        Assert.Equal("CustomerId", result.JoinInfo.SourceForeignKeyColumn); 
        Assert.Equal("CustomerId", result.JoinInfo.TargetPrimaryKeyColumn); 
        Assert.False(result.JoinInfo.IsOneToMany);
    }

    [Fact]
    public void NavigationPropertyAnalyzer_ShouldThrowForInvalidExpression()
    {
        Expression<Func<TestCustomer, string>> invalidExpression = c => c.Name;

        Assert.Throws<InvalidOperationException>(() => 
            NavigationPropertyAnalyzer.AnalyzeIncludeExpression(invalidExpression));
    }
}
