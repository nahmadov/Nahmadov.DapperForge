using Nahmadov.DapperForge.Core.Mutations.Bulk;

using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Bulk;

public class BulkOperationResultTests
{
    [Fact]
    public void BulkOperationResult_Properties_AreInitializedCorrectly()
    {
        var result = new BulkOperationResult
        {
            TotalAffected = 100,
            BatchCount = 2,
            ElapsedTime = TimeSpan.FromSeconds(1.5)
        };

        Assert.Equal(100, result.TotalAffected);
        Assert.Equal(2, result.BatchCount);
        Assert.Equal(TimeSpan.FromSeconds(1.5), result.ElapsedTime);
    }

    [Fact]
    public void BulkMergeResult_InheritsFromBulkOperationResult()
    {
        var result = new BulkMergeResult
        {
            TotalAffected = 50,
            BatchCount = 1,
            ElapsedTime = TimeSpan.FromMilliseconds(500),
            RowsInserted = 30,
            RowsUpdated = 20
        };

        Assert.Equal(50, result.TotalAffected);
        Assert.Equal(1, result.BatchCount);
        Assert.Equal(30, result.RowsInserted);
        Assert.Equal(20, result.RowsUpdated);
    }

    [Fact]
    public void BulkMergeResult_DefaultRowCounts_AreMinusOne()
    {
        var result = new BulkMergeResult();

        Assert.Equal(-1, result.RowsInserted);
        Assert.Equal(-1, result.RowsUpdated);
    }
}
