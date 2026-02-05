using Nahmadov.DapperForge.Core.Abstractions;

namespace Nahmadov.DapperForge.Core.Mutations.Bulk;

/// <summary>
/// Configuration options for bulk merge (upsert) operations.
/// </summary>
public class BulkMergeOptions
{
    /// <summary>
    /// Maximum number of entities per batch. Default: 100.
    /// Set to 0 for automatic calculation based on parameter limits.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Timeout in seconds for each batch. Default: 30.
    /// </summary>
    public int CommandTimeout { get; set; } = 30;

    /// <summary>
    /// Whether to validate all entities before starting the operation.
    /// Default: true.
    /// </summary>
    public bool ValidateBeforeMerge { get; set; } = true;

    /// <summary>
    /// The merge operation mode. Default: InsertOrUpdate.
    /// </summary>
    public MergeMode Mode { get; set; } = MergeMode.InsertOrUpdate;

    /// <summary>
    /// Column names to use for matching existing records.
    /// If null, uses the entity's effective key (primary or alternate key).
    /// </summary>
    public IReadOnlyList<string>? MatchColumns { get; set; }

    /// <summary>
    /// Column names to update when a match is found.
    /// If null, updates all non-key, non-generated columns.
    /// </summary>
    public IReadOnlyList<string>? UpdateColumns { get; set; }
}
