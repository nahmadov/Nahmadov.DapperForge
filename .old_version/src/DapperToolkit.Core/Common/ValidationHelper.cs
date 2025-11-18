using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace DapperToolkit.Core.Common;


public static class ValidationHelper
{
  public static void ValidateNotNull<T>([NotNull] T parameter, string parameterName) where T : class
  {
    if (parameter == null)
      throw new ArgumentNullException(parameterName);
  }

  public static void ValidateEntity<T>([NotNull] T entity, string operationName, string parameterName = "entity") where T : class
  {
    if (entity == null)
      throw new ArgumentNullException(parameterName, $"Entity of type {typeof(T).Name} cannot be null for {operationName} operation");
  }

  public static void ValidateExpression<T>([NotNull] Expression<T> expression, string parameterName) where T : class
  {
    if (expression == null)
      throw new ArgumentNullException(parameterName, $"Expression parameter '{parameterName}' cannot be null");
  }

  public static void ValidateRange(int value, int min, int max, string parameterName)
  {
    if (value < min || value > max)
      throw new ArgumentOutOfRangeException(parameterName, value, $"Value must be between {min} and {max}");
  }

  public static void ValidateGreaterThan(int value, int min, string parameterName)
  {
    if (value <= min)
      throw new ArgumentOutOfRangeException(parameterName, value, $"Value must be greater than {min}");
  }

  public static void ValidatePagination(int pageNumber, int pageSize)
  {
    ValidateGreaterThan(pageNumber, 0, nameof(pageNumber));
    ValidateGreaterThan(pageSize, 0, nameof(pageSize));
  }

  public static InvalidOperationException CreateEntityOperationException(Type entityType, string operationName, string reason, Exception? innerException = null)
  {
    var message = $"Operation '{operationName}' on entity '{entityType.Name}' failed: {reason}";
    return innerException != null
        ? new InvalidOperationException(message, innerException)
        : new InvalidOperationException(message);
  }


  public static NotSupportedException CreateNotSupportedException(string feature, string? context = null)
  {
    var message = context != null
        ? $"{feature} is not supported in {context}"
        : $"{feature} is not supported";
    return new NotSupportedException(message);
  }

  public static async Task<T> ExecuteDatabaseOperation<T>(Func<Task<T>> operation, string operationName, Type? entityType = null)
  {
    try
    {
      return await operation();
    }
    catch (Exception ex) when (IsDataException(ex))
    {
      var context = entityType != null ? $" on entity '{entityType.Name}'" : "";
      throw new InvalidOperationException($"Database operation '{operationName}'{context} failed: {ex.Message}", ex);
    }
  }

  public static T ExecuteDatabaseOperation<T>(Func<T> operation, string operationName, Type? entityType = null)
  {
    try
    {
      return operation();
    }
    catch (Exception ex) when (IsDataException(ex))
    {
      var context = entityType != null ? $" on entity '{entityType.Name}'" : "";
      throw new InvalidOperationException($"Database operation '{operationName}'{context} failed: {ex.Message}", ex);
    }
  }

  private static bool IsDataException(Exception ex)
  {
    var exceptionType = ex.GetType();
    var typeName = exceptionType.FullName ?? "";

    return typeName.Contains("Sql") ||
           typeName.Contains("Oracle") ||
           typeName.Contains("Data") ||
           typeName.Contains("Connection") ||
           typeName.Contains("Command") ||
           typeName.Contains("Transaction") ||
           ex is System.Data.Common.DbException ||
           ex is InvalidOperationException && ex.Message.Contains("connection");
  }
}
