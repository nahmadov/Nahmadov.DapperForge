namespace Nahmadov.DapperForge.Core.Exceptions;

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
