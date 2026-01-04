namespace Nahmadov.DapperForge.Core.Query;

internal sealed class SingleQueryPlan
{
    public required string Sql { get; init; }
    public required string SplitOn { get; init; }
    public required int MapTypesCount { get; init; }
}