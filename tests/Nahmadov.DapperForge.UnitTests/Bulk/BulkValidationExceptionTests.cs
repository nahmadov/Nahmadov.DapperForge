using Nahmadov.DapperForge.Core.Infrastructure.Exceptions;

using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Bulk;

public class BulkValidationExceptionTests
{
    [Fact]
    public void DapperBulkValidationException_SingleEntity_FormatsMessageCorrectly()
    {
        var errors = new Dictionary<int, IReadOnlyList<string>>
        {
            { 0, new[] { "Name is required" } }
        };

        var ex = new DapperBulkValidationException("User", errors);

        Assert.Contains("User", ex.Message);
        Assert.Contains("1 entity failed validation", ex.Message);
        Assert.Contains("[0]: Name is required", ex.Message);
    }

    [Fact]
    public void DapperBulkValidationException_MultipleEntities_FormatsMessageCorrectly()
    {
        var errors = new Dictionary<int, IReadOnlyList<string>>
        {
            { 0, new[] { "Name is required" } },
            { 2, new[] { "Email is invalid", "Name too long" } },
            { 5, new[] { "City is required" } }
        };

        var ex = new DapperBulkValidationException("Customer", errors);

        Assert.Contains("Customer", ex.Message);
        Assert.Contains("3 entities failed validation", ex.Message);
        Assert.Contains("[0]:", ex.Message);
        Assert.Contains("[2]:", ex.Message);
        Assert.Contains("[5]:", ex.Message);
    }

    [Fact]
    public void DapperBulkValidationException_MoreThanFiveErrors_Truncates()
    {
        var errors = new Dictionary<int, IReadOnlyList<string>>
        {
            { 0, new[] { "Error 1" } },
            { 1, new[] { "Error 2" } },
            { 2, new[] { "Error 3" } },
            { 3, new[] { "Error 4" } },
            { 4, new[] { "Error 5" } },
            { 5, new[] { "Error 6" } },
            { 6, new[] { "Error 7" } }
        };

        var ex = new DapperBulkValidationException("Product", errors);

        Assert.Contains("7 entities failed validation", ex.Message);
        Assert.Contains("... and 2 more", ex.Message);
    }

    [Fact]
    public void DapperBulkValidationException_Properties_AreSet()
    {
        var errors = new Dictionary<int, IReadOnlyList<string>>
        {
            { 0, new[] { "Error" } }
        };

        var ex = new DapperBulkValidationException("TestEntity", errors);

        Assert.Equal("TestEntity", ex.EntityName);
        Assert.Same(errors, ex.EntityErrors);
    }
}
