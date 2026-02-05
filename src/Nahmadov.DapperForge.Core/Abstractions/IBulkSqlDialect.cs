namespace Nahmadov.DapperForge.Core.Abstractions;

/// <summary>
/// Specifies the merge operation mode.
/// </summary>
public enum MergeMode
{
    /// <summary>Insert if not exists, update if exists.</summary>
    InsertOrUpdate,

    /// <summary>Insert only if not exists, skip existing records.</summary>
    InsertOnly,

    /// <summary>Update only if exists, skip new records.</summary>
    UpdateOnly
}

/// <summary>
/// Extends ISqlDialect with bulk operation SQL generation capabilities.
/// </summary>
public interface IBulkSqlDialect : ISqlDialect
{
    /// <summary>
    /// Gets the maximum number of parameters allowed in a single statement.
    /// SQL Server: ~2100, Oracle: ~32767
    /// </summary>
    int MaxParametersPerStatement { get; }

    /// <summary>
    /// Builds a bulk INSERT statement for multiple rows.
    /// </summary>
    /// <param name="tableName">The fully qualified table name.</param>
    /// <param name="columns">The column names to insert.</param>
    /// <param name="rowCount">The number of rows to insert.</param>
    /// <param name="parameterNameGenerator">Function to generate parameter names: (rowIndex, columnName) => parameterName.</param>
    /// <returns>The bulk INSERT SQL statement.</returns>
    string BuildBulkInsert(
        string tableName,
        IReadOnlyList<string> columns,
        int rowCount,
        Func<int, string, string> parameterNameGenerator);

    /// <summary>
    /// Builds a MERGE (UPSERT) statement for multiple rows.
    /// </summary>
    /// <param name="tableName">The fully qualified table name.</param>
    /// <param name="keyColumns">The columns used for matching existing records.</param>
    /// <param name="insertColumns">The columns to insert for new records.</param>
    /// <param name="updateColumns">The columns to update for existing records.</param>
    /// <param name="rowCount">The number of rows to merge.</param>
    /// <param name="parameterNameGenerator">Function to generate parameter names: (rowIndex, columnName) => parameterName.</param>
    /// <param name="mode">The merge operation mode.</param>
    /// <returns>The MERGE SQL statement.</returns>
    string BuildMerge(
        string tableName,
        IReadOnlyList<string> keyColumns,
        IReadOnlyList<string> insertColumns,
        IReadOnlyList<string> updateColumns,
        int rowCount,
        Func<int, string, string> parameterNameGenerator,
        MergeMode mode);
}
