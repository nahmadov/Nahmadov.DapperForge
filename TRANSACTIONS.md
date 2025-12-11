# Transaction Handling Guide

## Overview

DapperForge supports database transactions for coordinating multiple operations. This guide covers transaction basics, advanced patterns, and best practices.

---

## Basic Transaction Usage

### Simple Transaction Example

```csharp
public class OrderService
{
    private readonly AppDapperDbContext _db;

    public OrderService(AppDapperDbContext db)
    {
        _db = db;
    }

    public async Task<int> CreateOrderAsync(Order order, List<OrderItem> items)
    {
        // Begin transaction
        using (var transaction = await _db.BeginTransactionAsync())
        {
            try
            {
                // Insert order
                var orderId = await _db.Orders.InsertAndGetIdAsync<int>(order);
                order.Id = orderId;

                // Insert order items
                foreach (var item in items)
                {
                    item.OrderId = orderId;
                    await _db.OrderItems.InsertAsync(item);
                }

                // Commit transaction
                transaction.Commit();
                return orderId;
            }
            catch (Exception ex)
            {
                // Automatic rollback on exception
                transaction.Rollback();
                throw new DapperOperationException(
                    OperationType.Insert,
                    "Order",
                    $"Failed to create order: {ex.Message}",
                    ex);
            }
        }
    }
}
```

---

## Key Concepts

### 1. BeginTransactionAsync()

Starts a new database transaction on the connection.

```csharp
var transaction = await _db.BeginTransactionAsync();
```

**Returns:** `IDbTransaction` interface  
**Isolation Level:** Default (SQL Server: READ_COMMITTED)  
**State:** Active until Commit() or Rollback()

### 2. Passing Transaction to Operations

Pass the transaction to any operation:

```csharp
await _db.Orders.InsertAsync(order, transaction);
await _db.Users.UpdateAsync(user, transaction);
await _db.Products.DeleteByIdAsync(productId, transaction);
```

**Note:** DapperSet methods accept optional `transaction` parameter in their SQL execution.

### 3. Commit and Rollback

```csharp
transaction.Commit();    // Persist changes
transaction.Rollback();  // Discard changes
```

---

## Common Patterns

### Pattern 1: Insert with Related Data

```csharp
public async Task<int> CreateCustomerWithAddressAsync(
    Customer customer, 
    Address address)
{
    using (var tx = await _db.BeginTransactionAsync())
    {
        try
        {
            // Insert customer
            var customerId = await _db.Customers
                .InsertAndGetIdAsync<int>(customer);

            // Insert address linked to customer
            address.CustomerId = customerId;
            await _db.Addresses.InsertAsync(address);

            tx.Commit();
            return customerId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
```

---

### Pattern 2: Update Multiple Entities

```csharp
public async Task TransferFundsAsync(
    int fromAccountId, 
    int toAccountId, 
    decimal amount)
{
    using (var tx = await _db.BeginTransactionAsync())
    {
        try
        {
            // Get accounts
            var fromAccount = await _db.Accounts
                .FindAsync(fromAccountId);
            var toAccount = await _db.Accounts
                .FindAsync(toAccountId);

            // Validate
            if (fromAccount.Balance < amount)
                throw new InvalidOperationException("Insufficient funds");

            // Update balances
            fromAccount.Balance -= amount;
            toAccount.Balance += amount;

            await _db.Accounts.UpdateAsync(fromAccount);
            await _db.Accounts.UpdateAsync(toAccount);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
```

---

### Pattern 3: Insert with Validation

```csharp
public async Task<int> CreateProductAsync(
    Product product, 
    List<ProductAttribute> attributes)
{
    using (var tx = await _db.BeginTransactionAsync())
    {
        try
        {
            // Validate product
            if (string.IsNullOrWhiteSpace(product.Name))
                throw new DapperValidationException("Product", "Name is required");

            // Check for duplicate SKU
            var existing = await _db.Products
                .FirstOrDefaultAsync(p => p.Sku == product.Sku);
            if (existing != null)
                throw new InvalidOperationException("SKU already exists");

            // Insert product
            var productId = await _db.Products
                .InsertAndGetIdAsync<int>(product);

            // Insert attributes
            foreach (var attr in attributes)
            {
                attr.ProductId = productId;
                await _db.ProductAttributes.InsertAsync(attr);
            }

            tx.Commit();
            return productId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
```

---

### Pattern 4: Conditional Updates

```csharp
public async Task ApproveOrderAsync(int orderId)
{
    using (var tx = await _db.BeginTransactionAsync())
    {
        try
        {
            var order = await _db.Orders.FindAsync(orderId);

            if (order == null)
                throw new InvalidOperationException("Order not found");

            if (order.Status != "Pending")
                throw new InvalidOperationException("Only pending orders can be approved");

            // Update order
            order.Status = "Approved";
            order.ApprovedAt = DateTime.UtcNow;
            await _db.Orders.UpdateAsync(order);

            // Update inventory
            foreach (var item in await _db.OrderItems
                .WhereAsync(oi => oi.OrderId == orderId))
            {
                var product = await _db.Products.FindAsync(item.ProductId);
                product.StockQuantity -= item.Quantity;
                await _db.Products.UpdateAsync(product);
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
```

---

## Advanced Patterns

### Pattern 5: Nested Exception Handling

```csharp
public async Task ProcessBatchAsync(List<Order> orders)
{
    var successful = 0;
    var failed = 0;
    var errors = new List<string>();

    foreach (var order in orders)
    {
        using (var tx = await _db.BeginTransactionAsync())
        {
            try
            {
                // Process order
                await _db.Orders.InsertAsync(order);
                
                tx.Commit();
                successful++;
            }
            catch (DapperValidationException ex)
            {
                tx.Rollback();
                failed++;
                errors.Add($"Order {order.Id}: Validation failed - {string.Join(", ", ex.Errors)}");
            }
            catch (DapperConcurrencyException ex)
            {
                tx.Rollback();
                failed++;
                errors.Add($"Order {order.Id}: {ex.Message}");
            }
            catch (Exception ex)
            {
                tx.Rollback();
                failed++;
                errors.Add($"Order {order.Id}: {ex.Message}");
            }
        }
    }

    Console.WriteLine($"Processed {successful} successful, {failed} failed");
    if (errors.Any())
    {
        foreach (var error in errors)
            Console.WriteLine($"  - {error}");
    }
}
```

---

### Pattern 6: Transaction with Retry Logic

```csharp
public async Task<T> ExecuteWithRetryAsync<T>(
    Func<IDbTransaction, Task<T>> operation,
    int maxRetries = 3)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        using (var tx = await _db.BeginTransactionAsync())
        {
            try
            {
                var result = await operation(tx);
                tx.Commit();
                return result;
            }
            catch (DapperConcurrencyException) when (attempt < maxRetries)
            {
                tx.Rollback();
                await Task.Delay(100 * attempt); // Exponential backoff
                continue;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }

    throw new InvalidOperationException("Exceeded max retries");
}

// Usage
var result = await ExecuteWithRetryAsync(async tx =>
{
    await _db.Orders.InsertAsync(order);
    return order.Id;
});
```

---

## Best Practices

### 1. Always Use Using Statement

```csharp
// ✅ Good
using (var tx = await _db.BeginTransactionAsync())
{
    // Use tx
    tx.Commit();
}

// ❌ Bad - Transaction may not dispose properly
var tx = await _db.BeginTransactionAsync();
// Use tx
tx.Commit();
```

### 2. Wrap in Try-Catch

```csharp
// ✅ Good
using (var tx = await _db.BeginTransactionAsync())
{
    try
    {
        // Operations
        tx.Commit();
    }
    catch
    {
        tx.Rollback();
        throw;
    }
}

// ❌ Bad - No error handling
using (var tx = await _db.BeginTransactionAsync())
{
    // Operations
    tx.Commit(); // What if exception occurs?
}
```

### 3. Keep Transactions Short

```csharp
// ✅ Good - Transaction only covers DB operations
using (var tx = await _db.BeginTransactionAsync())
{
    try
    {
        var order = await _db.Orders.InsertAndGetIdAsync<int>(orderData);
        tx.Commit();
    }
    catch
    {
        tx.Rollback();
        throw;
    }
}

// ❌ Bad - Long-running operations in transaction
using (var tx = await _db.BeginTransactionAsync())
{
    var order = await _db.Orders.InsertAndGetIdAsync<int>(orderData);
    
    // Don't do long operations here!
    Thread.Sleep(5000); // Blocks transaction
    
    var email = await SendEmailAsync(order); // Network I/O
    
    tx.Commit();
}
```

### 4. Pass Transaction to All Operations

```csharp
// ✅ Good - All operations in same transaction
using (var tx = await _db.BeginTransactionAsync())
{
    try
    {
        await _db.Orders.InsertAsync(order, transaction: tx);
        await _db.OrderItems.InsertAsync(item, transaction: tx);
        tx.Commit();
    }
    catch
    {
        tx.Rollback();
        throw;
    }
}

// ❌ Bad - Mixed transactions
using (var tx = await _db.BeginTransactionAsync())
{
    await _db.Orders.InsertAsync(order, transaction: tx);
    await _db.OrderItems.InsertAsync(item); // No tx!
    tx.Commit();
}
```

### 5. Specific Exception Handling

```csharp
// ✅ Good - Handle specific exceptions
using (var tx = await _db.BeginTransactionAsync())
{
    try
    {
        await _db.Orders.InsertAsync(order);
        tx.Commit();
    }
    catch (DapperValidationException ex)
    {
        tx.Rollback();
        _logger.LogWarning("Validation failed: {Errors}", ex.Errors);
        throw;
    }
    catch (DapperConcurrencyException ex)
    {
        tx.Rollback();
        _logger.LogWarning("Concurrency error: {Message}", ex.Message);
        throw;
    }
    catch (Exception ex)
    {
        tx.Rollback();
        _logger.LogError("Unexpected error: {Message}", ex.Message);
        throw;
    }
}
```

### 6. Log Transaction Operations

```csharp
// ✅ Good - Log important operations
using (var tx = await _db.BeginTransactionAsync())
{
    _logger.LogInformation("Starting transaction for order {OrderId}", order.Id);
    
    try
    {
        var orderId = await _db.Orders.InsertAndGetIdAsync<int>(order);
        _logger.LogInformation("Inserted order {OrderId}", orderId);
        
        await _db.OrderItems.InsertAsync(item);
        _logger.LogInformation("Inserted order item {ItemId}", item.Id);
        
        tx.Commit();
        _logger.LogInformation("Committed transaction for order {OrderId}", orderId);
    }
    catch (Exception ex)
    {
        tx.Rollback();
        _logger.LogError(ex, "Transaction failed for order {OrderId}", order.Id);
        throw;
    }
}
```

---

## Common Pitfalls

### Pitfall 1: Forgetting to Commit

```csharp
// ❌ Bug - No commit!
using (var tx = await _db.BeginTransactionAsync())
{
    await _db.Orders.InsertAsync(order);
    // Changes are rolled back when using exits!
}
```

### Pitfall 2: Not Handling Validation Errors

```csharp
// ❌ Bug - No validation handling
using (var tx = await _db.BeginTransactionAsync())
{
    await _db.Orders.InsertAsync(order); // May throw DapperValidationException
    tx.Commit(); // Never reached!
}
```

### Pitfall 3: Nested Transactions

```csharp
// ❌ Bad - Nested transactions not supported
using (var tx1 = await _db.BeginTransactionAsync())
{
    await _db.Orders.InsertAsync(order, tx1);
    
    using (var tx2 = await _db.BeginTransactionAsync()) // ❌ Nested!
    {
        // ...
    }
}
```

### Pitfall 4: Not Using Same Connection

```csharp
// ❌ Risky - Different context instances
var context1 = new AppDapperDbContext(options);
var context2 = new AppDapperDbContext(options);

using (var tx = await context1.BeginTransactionAsync())
{
    await context1.Orders.InsertAsync(order, tx);
    await context2.Customers.UpdateAsync(customer, tx); // Different connection!
}
```

---

## Testing Transactions

### Unit Test Example

```csharp
[Fact]
public async Task CreateOrderAsync_WithValidData_InsertsOrderAndItems()
{
    // Arrange
    var order = new Order { CustomerId = 1, Total = 100 };
    var items = new List<OrderItem>
    {
        new OrderItem { ProductId = 1, Quantity = 2, Price = 50 }
    };
    var service = new OrderService(_dbContext);

    // Act
    var orderId = await service.CreateOrderAsync(order, items);

    // Assert
    Assert.True(orderId > 0);
    
    var insertedOrder = await _dbContext.Orders.FindAsync(orderId);
    Assert.NotNull(insertedOrder);
    Assert.Equal(100, insertedOrder.Total);

    var insertedItems = await _dbContext.OrderItems
        .WhereAsync(oi => oi.OrderId == orderId);
    Assert.Single(insertedItems);
}

[Fact]
public async Task TransferFundsAsync_WithInsufficientFunds_RollsBack()
{
    // Arrange
    var fromAccount = new Account { Id = 1, Balance = 50 };
    var toAccount = new Account { Id = 2, Balance = 100 };
    var service = new AccountService(_dbContext);

    // Act & Assert
    await Assert.ThrowsAsync<InvalidOperationException>(async () =>
    {
        await service.TransferFundsAsync(1, 2, 100); // Amount > balance
    });

    // Verify rollback
    var account = await _dbContext.Accounts.FindAsync(1);
    Assert.Equal(50, account.Balance); // Unchanged
}
```

---

## Limitations and Considerations

### Current Limitations

1. **No Savepoints** - DapperForge doesn't support nested transactions/savepoints
2. **Single Connection** - All operations must use same context instance
3. **Default Isolation Level** - Uses database default (typically READ_COMMITTED)
4. **Manual Commit** - Developer responsible for explicit commit

### Workarounds

For complex scenarios, consider:
- Stored procedures for multi-step operations
- Separate contexts for independent transaction scopes
- Application-level distributed transactions (if needed)

---

## Summary

✅ Simple transaction API  
✅ Full control with Commit/Rollback  
✅ Integrates with validation exceptions  
✅ Works with all CRUD operations  
✅ Explicit transaction management  

Transactions in DapperForge provide explicit, immediate control over database changes, aligning with the library's lightweight philosophy.
