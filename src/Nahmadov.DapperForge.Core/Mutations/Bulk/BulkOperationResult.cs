namespace Nahmadov.DapperForge.Core.Mutations.Bulk;

/// <summary>
/// Result of a bulk operation containing affected row counts and timing.
/// </summary>
public class BulkOperationResult
{
    /// <summary>
    /// Total number of rows affected across all batches.
    /// </summary>
    public int TotalAffected { get; init; }

    /// <summary>
    /// Number of batches executed.
    /// </summary>
    public int BatchCount { get; init; }

    /// <summary>
    /// Total execution time for all batches.
    /// </summary>
    public TimeSpan ElapsedTime { get; init; }
}

/// <summary>
/// Result of a bulk merge operation with insert/update breakdown.
/// </summary>
public class BulkMergeResult : BulkOperationResult
{
    /// <summary>
    /// Number of rows inserted (when supported by dialect).
    /// May be -1 if not available.
    /// </summary>
    public int RowsInserted { get; init; } = -1;

    /// <summary>
    /// Number of rows updated (when supported by dialect).
    /// May be -1 if not available.
    /// </summary>
    public int RowsUpdated { get; init; } = -1;
}
