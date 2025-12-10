# Exception Hierarchy Documentation

## Overview

DapperForge implements a comprehensive exception hierarchy to provide clear, actionable error messages for different failure scenarios. All exceptions inherit from `DapperForgeException`, allowing users to catch all DapperForge-related errors with a single catch block if needed.

---

## Exception Types

### 1. `DapperForgeException` (Base)

The root exception class for all DapperForge errors.

**Usage:**
```csharp
try
{
    // DapperForge operations
}
catch (DapperForgeException ex)
{
    // Catches any DapperForge error
    Console.WriteLine($"Error: {ex.Message}");
}
```

**Properties:**
- `Message` - Error description
- `InnerException` - Wrapped exception (if any)

---

### 2. `DapperValidationException`

Thrown when entity validation fails during insert or update operations.

**When thrown:**
- Required field is null or empty
- String exceeds maximum length
- String is shorter than minimum length
- Custom validation fails

**Properties:**
- `Errors` (IReadOnlyList<string>) - List of validation errors
- `Message` - Formatted validation message

**Example:**
```csharp
try
{
    var user = new User { Name = "" }; // Empty required field
    await dbContext.Users.InsertAsync(user);
}
catch (DapperValidationException ex)
{
    Console.WriteLine($"Validation failed: {ex.Message}");
    foreach (var error in ex.Errors)
    {
        Console.WriteLine($"  - {error}");
    }
}
```

**Output:**
```
Validation failed for entity 'User':
 - Property 'Name' is required and cannot be empty.
```

---

### 3. `DapperOperationException`

Base class for errors during database operations (insert, update, delete).

**Properties:**
- `OperationType` - Type of failed operation (Insert, Update, Delete, Query)
- `EntityName` - Name of the affected entity
- `Message` - Error description

---

### 4. `DapperConcurrencyException`

Thrown when an update or delete operation affects zero rows (optimistic concurrency failure).

**When thrown:**
- Update affects no rows
- Delete affects no rows
- Entity was likely modified/deleted by another transaction

**Example:**
```csharp
try
{
    var user = new User { Id = 999, Name = "Test" };
    await dbContext.Users.UpdateAsync(user); // Entity doesn't exist
}
catch (DapperConcurrencyException ex)
{
    Console.WriteLine($"Concurrency error: {ex.Message}");
    Console.WriteLine($"Operation: {ex.OperationType}");
    Console.WriteLine($"Entity: {ex.EntityName}");
}
```

**Output:**
```
Concurrency error: Update failed for entity 'User': no rows were affected. 
The entity may have been modified or deleted by another transaction.
Operation: Update
Entity: User
```

---

### 5. `DapperReadOnlyException`

Thrown when attempting to modify a read-only entity.

**When thrown:**
- Insert attempted on read-only entity
- Update attempted on read-only entity

**Example:**
```csharp
[ReadOnlyEntity]
public class ReadOnlyReport { ... }

try
{
    var report = new ReadOnlyReport { /* ... */ };
    await dbContext.Reports.InsertAsync(report);
}
catch (DapperReadOnlyException ex)
{
    Console.WriteLine($"Cannot modify: {ex.Message}");
    Console.WriteLine($"Operation: {ex.OperationType}");
}
```

**Output:**
```
Cannot modify: Entity 'ReadOnlyReport' is marked as ReadOnly and cannot be modified via Insert.
Operation: Insert
```

---

### 6. `DapperConfigurationException`

Thrown when entity configuration is invalid or incomplete.

**When thrown:**
- Entity has no key (required for Find, Update, Delete)
- SQL generation fails (missing table name, etc.)
- SQL is not configured for operation

**Example:**
```csharp
public class InvalidEntity { /* No [Key] attribute */ }

try
{
    var entity = new InvalidEntity();
    await dbContext.Set<InvalidEntity>().FindAsync(1);
}
catch (DapperConfigurationException ex)
{
    Console.WriteLine($"Configuration error: {ex.Message}");
    Console.WriteLine($"Entity: {ex.EntityName}");
}
```

**Output:**
```
Configuration error for entity 'InvalidEntity': Entity has no key and does not support FindAsync.
Entity: InvalidEntity
```

---

### 7. `OperationType` Enum

Specifies the type of database operation that failed.

**Values:**
- `Insert` - INSERT operation
- `Update` - UPDATE operation
- `Delete` - DELETE operation
- `Query` - SELECT/Query operation

---

## Exception Handling Patterns

### Catch by Specific Exception

```csharp
try
{
    await dbContext.Users.InsertAsync(user);
}
catch (DapperValidationException ex)
{
    // Handle validation errors
    logger.LogError("Validation failed: {@Errors}", ex.Errors);
}
catch (DapperConcurrencyException ex)
{
    // Handle concurrency issues
    logger.LogError("Concurrency error: {Message}", ex.Message);
}
catch (DapperForgeException ex)
{
    // Handle other DapperForge errors
    logger.LogError("DapperForge error: {Message}", ex.Message);
}
```

### Catch by Base Type

```csharp
try
{
    await dbContext.Users.UpdateAsync(user);
}
catch (DapperForgeException ex)
{
    // All DapperForge errors
    logger.LogError("Operation failed: {Message}", ex.Message);
}
```

### Handle with Custom Response

```csharp
public class UserController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateUserRequest request)
    {
        try
        {
            var user = new User { Name = request.Name, Email = request.Email };
            var id = await _db.Users.InsertAndGetIdAsync<int>(user);
            return Created($"/users/{id}", new { id });
        }
        catch (DapperValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (DapperForgeException ex)
        {
            logger.LogError("Database error: {Message}", ex.Message);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
```

---

## Best Practices

### 1. Log Validation Errors
```csharp
catch (DapperValidationException ex)
{
    logger.LogWarning("Validation failed for {Entity}: {@Errors}", 
        ex.Message, ex.Errors);
}
```

### 2. Distinguish Concurrency from Other Updates
```csharp
catch (DapperConcurrencyException ex)
{
    // Retry or notify user
    logger.LogWarning("Entity modified/deleted by another transaction");
}
catch (DapperOperationException ex)
{
    // Other operation errors
    logger.LogError("Operation {Type} failed: {Message}", 
        ex.OperationType, ex.Message);
}
```

### 3. Configuration Errors Should Be Caught Early
```csharp
// Bad: Fails at runtime
await dbContext.Set<BadEntity>().FindAsync(1);

// Good: Validate configuration during startup
var mapping = builder.Build();
if (mapping.KeyProperties.Count == 0)
    throw new DapperConfigurationException("BadEntity", "No key defined");
```

### 4. Provide User-Friendly Messages
```csharp
catch (DapperValidationException ex)
{
    var userMessage = ex.Errors.Count == 1
        ? $"Validation error: {ex.Errors[0]}"
        : $"Validation failed with {ex.Errors.Count} errors";
    
    return BadRequest(new { message = userMessage });
}
```

---

## Testing Exception Handling

### Unit Test with Exceptions

```csharp
[Fact]
public async Task InsertAsync_WithRequiredFieldNull_ThrowsDapperValidationException()
{
    var entity = new User { Name = null! };
    
    var ex = await Assert.ThrowsAsync<DapperValidationException>(
        () => _dbContext.Users.InsertAsync(entity));
    
    Assert.NotEmpty(ex.Errors);
    Assert.Contains("required", ex.Errors[0], StringComparison.OrdinalIgnoreCase);
}

[Fact]
public async Task UpdateAsync_WhenEntityDoesntExist_ThrowsDapperConcurrencyException()
{
    var entity = new User { Id = 999, Name = "Test" };
    
    var ex = await Assert.ThrowsAsync<DapperConcurrencyException>(
        () => _dbContext.Users.UpdateAsync(entity));
    
    Assert.Equal(OperationType.Update, ex.OperationType);
    Assert.Equal("User", ex.EntityName);
}
```

---

## Migration from System.ComponentModel.DataAnnotations.ValidationException

If you previously caught `System.ComponentModel.DataAnnotations.ValidationException`, update to:

```csharp
// Old
catch (System.ComponentModel.DataAnnotations.ValidationException ex)
{
    // ...
}

// New
catch (Nahmadov.DapperForge.Core.Exceptions.DapperValidationException ex)
{
    foreach (var error in ex.Errors)
    {
        // Process error
    }
}
```

---

## Summary

The exception hierarchy provides:
- ✅ Clear error categorization
- ✅ Actionable error messages
- ✅ Error details (validation errors, operation type, entity name)
- ✅ Type-safe exception handling
- ✅ Better debugging and logging

All exceptions contain sufficient context to diagnose and handle errors appropriately in your application.
