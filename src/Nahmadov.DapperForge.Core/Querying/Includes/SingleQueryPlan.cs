namespace Nahmadov.DapperForge.Core.Querying.Includes;

internal sealed class SingleQueryPlan
{
    public required string Sql { get; init; }
    public required string SplitOn { get; init; }
    public required int MapTypesCount { get; init; }

    /// <summary>
    /// Parameters produced by Include filter expressions (e.g. <c>.Include(x =&gt; x.Orders.Where(...))</c>).
    /// Merged with root-query parameters before execution.
    /// </summary>
    public IReadOnlyDictionary<string, object?> FilterParameters { get; init; }
        = new Dictionary<string, object?>();
}
