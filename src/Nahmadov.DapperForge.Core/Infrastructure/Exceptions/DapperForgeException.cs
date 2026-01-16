namespace Nahmadov.DapperForge.Core.Infrastructure.Exceptions;
/// <summary>
/// Base exception for all DapperForge-related errors.
/// </summary>
public class DapperForgeException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="DapperForgeException"/>.
    /// </summary>
    /// <param name="message">The error message.</param>
    public DapperForgeException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DapperForgeException"/> with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public DapperForgeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when entity validation fails during insert or update operations.
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="DapperValidationException"/>.
/// </remarks>
/// <param name="entityName">Name of the entity that failed validation.</param>
/// <param name="errors">List of validation error messages.</param>
public class DapperValidationException(string entityName, IEnumerable<string> errors) : DapperForgeException(FormatMessage(entityName, errors))
{
    /// <summary>
    /// Gets the list of validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; } = [.. errors];

    /// <summary>
    /// Initializes a new instance of <see cref="DapperValidationException"/> with a single error.
    /// </summary>
    /// <param name="entityName">Name of the entity that failed validation.</param>
    /// <param name="error">The validation error message.</param>
    public DapperValidationException(string entityName, string error)
          : this(entityName, [error])
    {
    }

    private static string FormatMessage(string entityName, IEnumerable<string> errors)
    {
        var errorList = string.Join("\n - ", errors);
        return $"Validation failed for entity '{entityName}':\n - {errorList}";
    }
}

/// <summary>
/// Exception thrown when a database operation (insert, update, delete) fails due to invalid state.
/// </summary>
public class DapperOperationException : DapperForgeException
{
    /// <summary>
    /// Gets the name of the entity involved in the failed operation.
    /// </summary>
    public string? EntityName { get; }

    /// <summary>
    /// Gets the type of operation that failed.
    /// </summary>
    public OperationType OperationType { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="DapperOperationException"/>.
    /// </summary>
    /// <param name="operationType">Type of the failed operation.</param>
    /// <param name="message">The error message.</param>
    public DapperOperationException(OperationType operationType, string message)
        : base(message)
    {
        OperationType = operationType;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DapperOperationException"/>.
    /// </summary>
    /// <param name="operationType">Type of the failed operation.</param>
    /// <param name="entityName">Name of the entity involved in the operation.</param>
    /// <param name="message">The error message.</param>
    public DapperOperationException(OperationType operationType, string entityName, string message)
        : base($"{message} (Entity: '{entityName}')")
    {
        OperationType = operationType;
        EntityName = entityName;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DapperOperationException"/> with an inner exception.
    /// </summary>
    /// <param name="operationType">Type of the failed operation.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public DapperOperationException(OperationType operationType, string message, Exception innerException)
        : base(message, innerException)
    {
        OperationType = operationType;
    }
}

/// <summary>
/// Exception thrown when no rows are affected by an update or delete operation.
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="DapperConcurrencyException"/>.
/// </remarks>
/// <param name="operationType">Type of the failed operation.</param>
/// <param name="entityName">Name of the entity involved in the operation.</param>
public class DapperConcurrencyException(OperationType operationType, string entityName) : DapperOperationException(operationType, entityName,
        $"{operationType} failed for entity '{entityName}': no rows were affected. " +
            "The entity may have been modified or deleted by another transaction.")
{
}

/// <summary>
/// Exception thrown when an entity configuration is invalid.
/// </summary>
public class DapperConfigurationException : DapperForgeException
{
    /// <summary>
    /// Gets the name of the entity with invalid configuration.
    /// </summary>
    public string? EntityName { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="DapperConfigurationException"/>.
    /// </summary>
    /// <param name="message">The error message.</param>
    public DapperConfigurationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DapperConfigurationException"/>.
    /// </summary>
    /// <param name="entityName">Name of the entity with invalid configuration.</param>
    /// <param name="message">The error message.</param>
    public DapperConfigurationException(string entityName, string message)
        : base($"Configuration error for entity '{entityName}': {message}")
    {
        EntityName = entityName;
    }
}

/// <summary>
/// Exception thrown when an operation is performed on a read-only entity.
/// </summary>
public class DapperReadOnlyException : DapperOperationException
{
    /// <summary>
    /// Initializes a new instance of <see cref="DapperReadOnlyException"/>.
    /// </summary>
    /// <param name="operationType">Type of the operation attempted.</param>
    /// <param name="entityName">Name of the read-only entity.</param>
    public DapperReadOnlyException(OperationType operationType, string entityName)
        : base(operationType, entityName,
            $"Entity '{entityName}' is marked as ReadOnly and cannot be modified via {operationType}.")
    {
    }
}

/// <summary>
/// Exception thrown when a database command execution fails.
/// Wraps the underlying database exception with operation context.
/// </summary>
public class DapperExecutionException : DapperOperationException
{
    /// <summary>
    /// Gets the SQL that was being executed when the error occurred.
    /// </summary>
    public string? Sql { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="DapperExecutionException"/>.
    /// </summary>
    /// <param name="operationType">Type of the failed operation.</param>
    /// <param name="entityName">Name of the entity involved.</param>
    /// <param name="sql">SQL statement that failed.</param>
    /// <param name="innerException">The underlying database exception.</param>
    public DapperExecutionException(
        OperationType operationType,
        string entityName,
        string? sql,
        Exception innerException)
        : base(operationType, FormatMessage(operationType, entityName, sql, innerException), innerException)
    {
        EntityName = entityName;
        Sql = sql;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DapperExecutionException"/> without entity context.
    /// </summary>
    /// <param name="operationType">Type of the failed operation.</param>
    /// <param name="sql">SQL statement that failed.</param>
    /// <param name="innerException">The underlying database exception.</param>
    public DapperExecutionException(
        OperationType operationType,
        string? sql,
        Exception innerException)
        : base(operationType, FormatMessage(operationType, null, sql, innerException), innerException)
    {
        Sql = sql;
    }

    private static string FormatMessage(OperationType operationType, string? entityName, string? sql, Exception inner)
    {
        var entityPart = string.IsNullOrEmpty(entityName) ? "" : $" on entity '{entityName}'";
        var sqlPart = string.IsNullOrEmpty(sql) ? "" : $"\nSQL: {TruncateSql(sql)}";
        return $"{operationType} operation failed{entityPart}: {inner.Message}{sqlPart}";
    }

    private static string TruncateSql(string sql)
    {
        const int maxLength = 500;
        return sql.Length <= maxLength ? sql : sql[..maxLength] + "...";
    }

    /// <summary>
    /// Gets the name of the entity involved in the failed operation.
    /// </summary>
    public new string? EntityName { get; }
}

/// <summary>
/// Exception thrown when setting a generated key value on an entity fails.
/// This is a non-critical error - the insert succeeded but the key could not be assigned.
/// </summary>
public class DapperKeyAssignmentException : DapperForgeException
{
    /// <summary>
    /// Gets the name of the entity.
    /// </summary>
    public string EntityName { get; }

    /// <summary>
    /// Gets the name of the key property.
    /// </summary>
    public string KeyPropertyName { get; }

    /// <summary>
    /// Gets the generated key value that could not be assigned.
    /// </summary>
    public object? KeyValue { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="DapperKeyAssignmentException"/>.
    /// </summary>
    public DapperKeyAssignmentException(
        string entityName,
        string keyPropertyName,
        object? keyValue,
        Exception innerException)
        : base($"Failed to assign generated key '{keyPropertyName}' with value '{keyValue}' to entity '{entityName}'. " +
               $"The insert succeeded but the entity's key property was not updated. Error: {innerException.Message}",
               innerException)
    {
        EntityName = entityName;
        KeyPropertyName = keyPropertyName;
        KeyValue = keyValue;
    }
}

/// <summary>
/// Exception thrown when a connection cannot be established or is in an invalid state.
/// </summary>
public class DapperConnectionException : DapperForgeException
{
    /// <summary>
    /// Initializes a new instance of <see cref="DapperConnectionException"/>.
    /// </summary>
    /// <param name="message">The error message.</param>
    public DapperConnectionException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DapperConnectionException"/> with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public DapperConnectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Enumerates the types of database operations.
/// </summary>
public enum OperationType
{
    /// <summary>Insert operation.</summary>
    Insert,

    /// <summary>Update operation.</summary>
    Update,

    /// <summary>Delete operation.</summary>
    Delete,

    /// <summary>Query/Select operation.</summary>
    Query
}

