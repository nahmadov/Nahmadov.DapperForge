using System.Reflection;

using DapperToolkit.Core.Attributes;
using DapperToolkit.Oracle.Context;

namespace DapperToolkit.OracleTests;

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
}