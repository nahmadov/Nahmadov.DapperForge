namespace Nahmadov.DapperForge.Core.Mutations.Bulk;

/// <summary>
/// Options controlling a bulk-copy operation. Some options are specific to SQL Server's
/// <c>SqlBulkCopy</c> and are ignored by the SQLite batched-insert fallback.
/// </summary>
public sealed class BulkCopyOptions
{
    /// <summary>
    /// Number of rows per batch. <c>0</c> (default) lets the provider choose:
    /// SQL Server streams in a single operation; the SQLite fallback fills each statement up to the
    /// parameter limit.
    /// </summary>
    public int BatchSize { get; set; }

    /// <summary>Command/operation timeout in seconds. Default 30.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Enables streaming from the data source (SQL Server <c>SqlBulkCopy.EnableStreaming</c>).
    /// Ignored by the SQLite fallback.
    /// </summary>
    public bool EnableStreaming { get; set; }

    /// <summary>
    /// Acquires a bulk-update table lock for the duration of the copy (SQL Server
    /// <c>SqlBulkCopyOptions.TableLock</c>). Ignored by the SQLite fallback.
    /// </summary>
    public bool UseTableLock { get; set; }
}
