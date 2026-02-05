namespace Nahmadov.DapperForge.Core.Mutations.Bulk;

/// <summary>
/// Configuration options for bulk insert operations.
/// </summary>
public class BulkInsertOptions
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
    public bool ValidateBeforeInsert { get; set; } = true;
}
