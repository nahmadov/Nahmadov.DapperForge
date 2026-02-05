using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Mutations.Bulk;

using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Bulk;

public class BulkOptionsTests
{
    #region BulkInsertOptions Tests

    [Fact]
    public void BulkInsertOptions_DefaultValues_AreCorrect()
    {
        var options = new BulkInsertOptions();

        Assert.Equal(100, options.BatchSize);
        Assert.Equal(30, options.CommandTimeout);
        Assert.True(options.ValidateBeforeInsert);
    }

    [Fact]
    public void BulkInsertOptions_CanBeCustomized()
    {
        var options = new BulkInsertOptions
        {
            BatchSize = 500,
            CommandTimeout = 60,
            ValidateBeforeInsert = false
        };

        Assert.Equal(500, options.BatchSize);
        Assert.Equal(60, options.CommandTimeout);
        Assert.False(options.ValidateBeforeInsert);
    }

    #endregion

    #region BulkMergeOptions Tests

    [Fact]
    public void BulkMergeOptions_DefaultValues_AreCorrect()
    {
        var options = new BulkMergeOptions();

        Assert.Equal(100, options.BatchSize);
        Assert.Equal(30, options.CommandTimeout);
        Assert.True(options.ValidateBeforeMerge);
        Assert.Equal(MergeMode.InsertOrUpdate, options.Mode);
        Assert.Null(options.MatchColumns);
        Assert.Null(options.UpdateColumns);
    }

    [Fact]
    public void BulkMergeOptions_CanBeCustomized()
    {
        var matchCols = new[] { "Email" };
        var updateCols = new[] { "Name", "Phone" };

        var options = new BulkMergeOptions
        {
            BatchSize = 200,
            CommandTimeout = 120,
            ValidateBeforeMerge = false,
            Mode = MergeMode.InsertOnly,
            MatchColumns = matchCols,
            UpdateColumns = updateCols
        };

        Assert.Equal(200, options.BatchSize);
        Assert.Equal(120, options.CommandTimeout);
        Assert.False(options.ValidateBeforeMerge);
        Assert.Equal(MergeMode.InsertOnly, options.Mode);
        Assert.Same(matchCols, options.MatchColumns);
        Assert.Same(updateCols, options.UpdateColumns);
    }

    #endregion

    #region MergeMode Tests

    [Fact]
    public void MergeMode_InsertOrUpdate_IsDefault()
    {
        var mode = default(MergeMode);

        Assert.Equal(MergeMode.InsertOrUpdate, mode);
    }

    [Fact]
    public void MergeMode_HasAllExpectedValues()
    {
        Assert.Equal(0, (int)MergeMode.InsertOrUpdate);
        Assert.Equal(1, (int)MergeMode.InsertOnly);
        Assert.Equal(2, (int)MergeMode.UpdateOnly);
    }

    #endregion
}
