# DapperForge - Usage Guide

This guide provides comprehensive examples of using DapperForge in your applications.

## Table of Contents

1. [Setup and Configuration](#setup-and-configuration)
2. [Entity Configuration](#entity-configuration)
3. [Query Operations](#query-operations)
4. [Mutation Operations](#mutation-operations)
5. [Include/ThenInclude](#includetheninclude)
6. [Transactions](#transactions)
7. [Validation](#validation)
8. [Advanced Scenarios](#advanced-scenarios)

## Setup and Configuration

### 1. Install NuGet Packages

```bash
dotnet add package Nahmadov.DapperForge.Core
dotnet add package Nahmadov.DapperForge.SqlServer
# OR
dotnet add package Nahmadov.DapperForge.Oracle
```

### 2. Define Your Entities

```csharp
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
    public bool IsActive { get; set; }

    // Navigation properties
    public Address Address { get; set; }
    public ICollection<Order> Orders { get; set; }
}

public class Address
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Street { get; set; }
    public string City { get; set; }
    public string ZipCode { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public decimal Total { get; set; }
    public DateTime OrderDate { get; set; }

    // Navigation properties
    public Customer Customer { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; }
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }

    // Navigation properties
    public Order Order { get; set; }
    public Product Product { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}
```

### 3. Create Your DbContext

```csharp
public class AppDbContext : DapperDbContext
{
    public DapperSet<Customer> Customers => Set<Customer>();
    public DapperSet<Order> Orders => Set<Order>();
    public DapperSet<OrderItem> OrderItems => Set<OrderItem>();
    public DapperSet<Product> Products => Set<Product>();
    public DapperSet<Address> Addresses => Set<Address>();

    public AppDbContext(DapperDbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(DapperModelBuilder modelBuilder)
    {
        // Configure entities
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
```

### 4. Register in Dependency Injection

```csharp
// In Program.cs or Startup.cs
services.AddDapperDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"));
});

// OR for Oracle
services.AddDapperDbContext<AppDbContext>(options =>
{
    options.UseOracle(Configuration.GetConnectionString("OracleConnection"));
});
```

## Entity Configuration

### Using Fluent API

```csharp
public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        // Table mapping
        builder.ToTable("Customers", "dbo");

        // Primary key
        builder.HasKey(c => c.Id);

        // Property configurations
        builder.Property(c => c.Name)
            .HasColumnName("FullName")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Email)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .IsReadOnly(); // Cannot be updated

        builder.Property(c => c.LastLogin)
            .IsReadOnly(); // Read-only, auto-updated by database
    }
}
```

### Using Attributes

```csharp
[Table("Customers", Schema = "dbo")]
public class Customer
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("FullName")]
    public string Name { get; set; }

    [Required]
    [MaxLength(150)]
    public string Email { get; set; }

    [ReadOnly]
    public DateTime CreatedAt { get; set; }

    [ForeignKey(nameof(Address))]
    public int? AddressId { get; set; }

    public Address Address { get; set; }
}
```

### Applying Configurations

#### Option 1: Manually Apply Each Configuration

```csharp
protected override void OnModelCreating(DapperModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfiguration(new CustomerConfiguration());
    modelBuilder.ApplyConfiguration(new OrderConfiguration());
    modelBuilder.ApplyConfiguration(new ProductConfiguration());
}
```

#### Option 2: Auto-discover from Assembly

```csharp
protected override void OnModelCreating(DapperModelBuilder modelBuilder)
{
    // Applies all IEntityTypeConfiguration<T> implementations
    modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
}
```

#### Option 3: Inline Configuration

```csharp
protected override void OnModelCreating(DapperModelBuilder modelBuilder)
{
    modelBuilder.Entity<Customer>(b =>
    {
        b.ToTable("Customers", "dbo");
        b.HasKey(c => c.Id);
        b.Property(c => c.Name).HasMaxLength(100).IsRequired();
        b.Property(c => c.Email).HasMaxLength(150).IsRequired();
    });
}
```

## Query Operations

### Simple Queries

```csharp
// Get all entities
var allCustomers = await db.Customers.GetAllAsync();

// Find by ID
var customer = await db.Customers.FindAsync(123);

// Check if exists
bool hasActiveCustomers = await db.Customers.AnyAsync(c => c.IsActive);

// Count
long activeCount = await db.Customers.CountAsync(c => c.IsActive);
```

### Filtered Queries

```csharp
// Simple filter
var activeCustomers = await db.Customers.WhereAsync(c => c.IsActive);

// Complex filter
var results = await db.Customers.WhereAsync(c =>
    c.IsActive && c.Name.StartsWith("John") && c.CreatedAt > DateTime.Now.AddDays(-30));

// Case-insensitive filter
var customers = await db.Customers.WhereAsync(
    c => c.Name.Contains("john"),
    ignoreCase: true);
```

### Fluent Query API

```csharp
// Basic query
var customers = await db.Customers
    .Where(c => c.IsActive)
    .OrderBy(c => c.Name)
    .ToListAsync();

// Pagination
var pagedCustomers = await db.Customers
    .Where(c => c.IsActive)
    .OrderBy(c => c.Name)
    .Skip(20)
    .Take(10)
    .ToListAsync();

// Multiple ordering
var customers = await db.Customers
    .OrderBy(c => c.Name)
    .ThenByDescending(c => c.CreatedAt)
    .ToListAsync();

// Distinct
var distinctNames = await db.Customers
    .Distinct()
    .ToListAsync();
```

### First/Single Operations

```csharp
// First (throws if not found)
var customer = await db.Customers
    .Where(c => c.Email == email)
    .FirstAsync();

// FirstOrDefault (returns null if not found)
var customer = await db.Customers
    .Where(c => c.Email == email)
    .FirstOrDefaultAsync();

// Single (throws if zero or multiple results)
var customer = await db.Customers
    .Where(c => c.Id == 123)
    .SingleAsync();

// Last
var lastCustomer = await db.Customers
    .OrderBy(c => c.CreatedAt)
    .LastAsync();
```

### Expression Translation Examples

```csharp
// Comparisons
var results = await db.Customers.WhereAsync(c => c.Age > 18);
// WHERE a.[Age] > @Age

// String methods
var results = await db.Customers.WhereAsync(c => c.Name.StartsWith("John"));
// WHERE a.[Name] LIKE @p0  (parameter: "John%")

var results = await db.Customers.WhereAsync(c => c.Name.Contains("doe"));
// WHERE a.[Name] LIKE @p0  (parameter: "%doe%")

// Boolean properties
var results = await db.Customers.WhereAsync(c => c.IsActive);
// WHERE a.[IsActive] = 1

var results = await db.Customers.WhereAsync(c => !c.IsActive);
// WHERE a.[IsActive] = 0

// Null checks
var results = await db.Customers.WhereAsync(c => c.Email == null);
// WHERE a.[Email] IS NULL

// Collection Contains (IN clause)
var ids = new[] { 1, 2, 3, 4, 5 };
var results = await db.Customers.WhereAsync(c => ids.Contains(c.Id));
// WHERE a.[Id] IN (1, 2, 3, 4, 5)

// Logical operators
var results = await db.Customers.WhereAsync(c =>
    c.IsActive && (c.Name.StartsWith("John") || c.Name.StartsWith("Jane")));
// WHERE a.[IsActive] = 1 AND (a.[Name] LIKE @p0 OR a.[Name] LIKE @p1)
```

## Mutation Operations

### Insert

```csharp
// Simple insert
var customer = new Customer
{
    Name = "John Doe",
    Email = "john@example.com",
    CreatedAt = DateTime.UtcNow,
    IsActive = true
};

var rowsAffected = await db.Customers.InsertAsync(customer);

// Insert and get generated ID
var customerId = await db.Customers.InsertAndGetIdAsync<int>(customer);
```

### Update

```csharp
// Update entity
var customer = await db.Customers.FindAsync(123);
customer.Name = "Jane Doe";
customer.Email = "jane@example.com";

var rowsAffected = await db.Customers.UpdateAsync(customer);

// Mass update with explicit WHERE
await db.Customers.UpdateAsync(
    new Customer { IsActive = false },
    where: new { CreatedAt = DateTime.UtcNow.AddYears(-5) },
    allowMultiple: true,
    expectedRows: null // Don't check affected rows
);
```

### Delete

```csharp
// Delete by entity
var customer = await db.Customers.FindAsync(123);
var rowsAffected = await db.Customers.DeleteAsync(customer);

// Delete by ID
var rowsAffected = await db.Customers.DeleteByIdAsync(123);

// Mass delete with explicit WHERE
await db.Customers.DeleteAsync(
    where: new { IsActive = false },
    allowMultiple: true,
    expectedRows: null
);
```

### Validation Exceptions

```csharp
try
{
    var customer = new Customer
    {
        Name = null, // Required field
        Email = "a".PadRight(200, 'a') // Exceeds max length
    };

    await db.Customers.InsertAsync(customer);
}
catch (DapperValidationException ex)
{
    // ex.Errors contains all validation failures
    foreach (var error in ex.Errors)
    {
        Console.WriteLine($"{error.PropertyName}: {error.ErrorMessage}");
    }
}
```

## Include/ThenInclude

### Simple Include

```csharp
// Include single navigation property
var customers = await db.Customers
    .Include(c => c.Address)
    .ToListAsync();
```

### ThenInclude (Nested Relationships)

```csharp
// Include nested relationships
var orders = await db.Orders
    .Include(o => o.Customer)
        .ThenInclude(c => c.Address)
    .ToListAsync();
```

### Multiple Includes

```csharp
// Include multiple navigation properties
var orders = await db.Orders
    .Include(o => o.Customer)
        .ThenInclude(c => c.Address)
    .Include(o => o.OrderItems)
        .ThenInclude(i => i.Product)
    .ToListAsync();
```

### Query Splitting Strategies

#### AsSingleQuery (Default for Simple Includes)

```csharp
// Single query with JOINs
var orders = await db.Orders
    .Include(o => o.Customer)
    .AsSingleQuery()
    .ToListAsync();

// Generates:
// SELECT o.*, c.*
// FROM Orders o
// LEFT JOIN Customers c ON o.CustomerId = c.Id
```

**Use when:**
- Single navigation property
- Small result sets
- One-to-one relationships

#### AsSplitQuery (Recommended for Collections)

```csharp
// Multiple queries with IN clauses
var orders = await db.Orders
    .Include(o => o.Customer)
        .ThenInclude(c => c.Address)
    .Include(o => o.OrderItems)
        .ThenInclude(i => i.Product)
    .AsSplitQuery()
    .ToListAsync();

// Generates:
// Query 1: SELECT * FROM Orders
// Query 2: SELECT * FROM Customers WHERE Id IN (...)
// Query 3: SELECT * FROM Addresses WHERE CustomerId IN (...)
// Query 4: SELECT * FROM OrderItems WHERE OrderId IN (...)
// Query 5: SELECT * FROM Products WHERE Id IN (...)
```

**Use when:**
- Collection navigation properties
- Large result sets
- Multiple includes
- Avoiding cartesian products

### Identity Resolution

```csharp
// Default: Prevents duplicate entity instances
var customers = await db.Customers
    .Include(c => c.Orders)
    .ToListAsync();
// Customer instance reused across all orders

// Disable identity resolution (creates new instances)
var customers = await db.Customers
    .Include(c => c.Orders)
    .AsNoIdentityResolution()
    .ToListAsync();
// New Customer instance per order
```

## Transactions

### Basic Transaction

```csharp
var scope = await db.BeginTransactionScopeAsync();
try
{
    await db.Customers.InsertAsync(customer, scope.Transaction);
    await db.Orders.InsertAsync(order, scope.Transaction);

    scope.Complete(); // Mark for commit
}
finally
{
    scope.Dispose(); // Commits if Complete() called, else rolls back
}
```

### Transaction with Isolation Level

```csharp
var scope = await db.BeginTransactionScopeAsync(IsolationLevel.ReadCommitted);
try
{
    // Your operations
    scope.Complete();
}
finally
{
    scope.Dispose();
}
```

### Manual Commit/Rollback

```csharp
var scope = await db.BeginTransactionScopeAsync();
try
{
    await db.Customers.InsertAsync(customer, scope.Transaction);

    if (someCondition)
    {
        scope.Commit(); // Manual commit
    }
    else
    {
        scope.Rollback(); // Manual rollback
    }
}
finally
{
    scope.Dispose();
}
```

### Nested Operations

```csharp
var scope = await db.BeginTransactionScopeAsync();
try
{
    // Insert customer
    var customerId = await db.Customers.InsertAndGetIdAsync<int>(customer, scope.Transaction);

    // Insert related orders
    foreach (var order in orders)
    {
        order.CustomerId = customerId;
        await db.Orders.InsertAsync(order, scope.Transaction);
    }

    scope.Complete();
}
finally
{
    scope.Dispose();
}
```

## Validation

### Fluent Configuration Validation

```csharp
modelBuilder.Entity<Customer>(b =>
{
    b.Property(c => c.Name)
        .IsRequired()
        .HasMaxLength(100);

    b.Property(c => c.Email)
        .IsRequired()
        .HasMaxLength(150);
});

// Throws DapperValidationException if validation fails
await db.Customers.InsertAsync(customer);
```

### Data Annotation Validation

```csharp
public class Customer
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; }

    [Required]
    [MaxLength(150)]
    [EmailAddress]
    public string Email { get; set; }

    [StringLength(20, MinimumLength = 10)]
    public string PhoneNumber { get; set; }
}
```

### Read-Only Entities

```csharp
[ReadOnlyEntity]
public class AuditLog
{
    public int Id { get; set; }
    public string Action { get; set; }
    public DateTime Timestamp { get; set; }
}

// OR using fluent API
modelBuilder.Entity<AuditLog>(b =>
{
    b.IsReadOnly();
});

// Throws DapperReadOnlyException
await db.AuditLogs.InsertAsync(log);
await db.AuditLogs.UpdateAsync(log);
await db.AuditLogs.DeleteAsync(log);
```

### Read-Only Properties

```csharp
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }

    [ReadOnly]
    public DateTime CreatedAt { get; set; } // Excluded from INSERT/UPDATE

    [ReadOnly]
    public DateTime? LastLogin { get; set; } // Excluded from INSERT/UPDATE
}
```

## Advanced Scenarios

### Raw SQL Queries

```csharp
// Execute raw SQL
var customers = await db.QueryAsync<Customer>(
    "SELECT * FROM Customers WHERE IsActive = @IsActive",
    new { IsActive = true });

// Execute scalar query
var count = await db.QueryFirstOrDefaultAsync<int>(
    "SELECT COUNT(*) FROM Customers WHERE IsActive = @IsActive",
    new { IsActive = true });

// Execute non-query
var rowsAffected = await db.ExecuteAsync(
    "UPDATE Customers SET IsActive = 0 WHERE CreatedAt < @Date",
    new { Date = DateTime.UtcNow.AddYears(-5) });
```

### Connection Scopes

```csharp
// Manual connection management
using (var connectionScope = db.CreateConnectionScope())
{
    var connection = connectionScope.Connection;

    // Use connection for multiple operations
    await connection.QueryAsync<Customer>("SELECT * FROM Customers");
    await connection.ExecuteAsync("UPDATE Customers SET ...");
}
```

### Alternate Keys

```csharp
// Configure alternate key (business key)
modelBuilder.Entity<Customer>(b =>
{
    b.HasKey(c => c.Id); // Primary key
    b.HasAlternateKey(c => c.Email); // Alternate key for lookups
});

// Operations will use alternate key when primary key is not available
var customer = new Customer { Email = "john@example.com", Name = "John" };
await db.Customers.UpdateAsync(customer); // Uses Email for WHERE clause
```

### Oracle Sequences

```csharp
// Configure Oracle sequence
modelBuilder.Entity<Customer>(b =>
{
    b.Property(c => c.Id).UseSequence("CUSTOMER_SEQ");
});

// INSERT will use: CUSTOMER_SEQ.NEXTVAL
await db.Customers.InsertAsync(customer);
```

### Health Checks

```csharp
// Check database connectivity
bool isHealthy = await db.HealthCheckAsync();

if (!isHealthy)
{
    // Handle database connectivity issues
}
```

### Retry Logic

The query executor automatically retries transient failures:

```csharp
// Automatic retry for transient failures
var customers = await db.Customers.GetAllAsync();
// If timeout occurs, automatically retries with exponential backoff:
// Retry 1: Wait 100ms
// Retry 2: Wait 200ms
// Retry 3: Wait 400ms
```

**Transient Errors Detected:**
- SQL Server: Timeout (-2), Deadlock (1205), Azure transient codes
- Oracle: Deadlock (ORA-00060), Cancel (ORA-01013)

**Note:** Mutations (INSERT/UPDATE/DELETE) are NOT retried automatically.

### Logging

```csharp
// Enable SQL logging
services.AddDapperDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    options.EnableSqlLogging = true;
    options.Logger = loggerFactory.CreateLogger<AppDbContext>();
});
```

### Custom Type Handlers

```csharp
// Register custom Dapper type handler
SqlMapper.AddTypeHandler(new JsonTypeHandler());

public class JsonTypeHandler : SqlMapper.TypeHandler<JObject>
{
    public override void SetValue(IDbDataParameter parameter, JObject value)
    {
        parameter.Value = value?.ToString();
    }

    public override JObject Parse(object value)
    {
        return JObject.Parse(value.ToString());
    }
}
```

### Bulk Operations Pattern

```csharp
// Pattern for bulk inserts
var customers = GetCustomersToInsert(); // 1000+ items

var scope = await db.BeginTransactionScopeAsync();
try
{
    foreach (var customer in customers)
    {
        await db.Customers.InsertAsync(customer, scope.Transaction);
    }

    scope.Complete();
}
finally
{
    scope.Dispose();
}

// For true bulk operations, use SqlBulkCopy or Oracle bulk APIs directly
```

## Best Practices

### 1. Use Scoped Lifetime

```csharp
// ✅ Correct
services.AddDapperDbContext<AppDbContext>(
    options => options.UseSqlServer(connectionString),
    ServiceLifetime.Scoped);

// ❌ Incorrect - causes connection pool exhaustion
services.AddDapperDbContext<AppDbContext>(
    options => options.UseSqlServer(connectionString),
    ServiceLifetime.Singleton);
```

### 2. Prefer Split Query for Collections

```csharp
// ✅ Correct - avoids cartesian product
var orders = await db.Orders
    .Include(o => o.OrderItems)
    .AsSplitQuery()
    .ToListAsync();

// ❌ Avoid - can cause cartesian explosion
var orders = await db.Orders
    .Include(o => o.OrderItems)
    .AsSingleQuery()
    .ToListAsync();
```

### 3. Use Transactions for Related Operations

```csharp
// ✅ Correct - atomic operation
var scope = await db.BeginTransactionScopeAsync();
try
{
    await db.Customers.InsertAsync(customer, scope.Transaction);
    await db.Orders.InsertAsync(order, scope.Transaction);
    scope.Complete();
}
finally
{
    scope.Dispose();
}

// ❌ Avoid - not atomic
await db.Customers.InsertAsync(customer);
await db.Orders.InsertAsync(order); // Could fail leaving orphaned customer
```

### 4. Validate Before Insert/Update

DapperForge automatically validates based on configuration, but you can add custom validation:

```csharp
// ✅ Good practice
if (string.IsNullOrEmpty(customer.Email))
{
    throw new ValidationException("Email is required");
}

await db.Customers.InsertAsync(customer);
```

### 5. Use FirstOrDefaultAsync Instead of GetAllAsync

```csharp
// ✅ Efficient
var customer = await db.Customers.FirstOrDefaultAsync(c => c.Email == email);

// ❌ Inefficient - loads all records into memory
var customer = (await db.Customers.GetAllAsync())
    .FirstOrDefault(c => c.Email == email);
```

### 6. Leverage Expression Caching

```csharp
// ✅ Expression cached and reused
Expression<Func<Customer, bool>> activeFilter = c => c.IsActive;
var customers1 = await db.Customers.WhereAsync(activeFilter);
var customers2 = await db.Customers.WhereAsync(activeFilter); // Cache hit
```

## Next Steps

- [Architecture Documentation](./DapperForge-Architecture.md) - Understand internal architecture
- [Complete Reference](./DapperForge-Complete-Reference.md) - Full API reference
