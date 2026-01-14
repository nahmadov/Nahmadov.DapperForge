# DapperForge - Overview

## What is DapperForge?

**Nahmadov.DapperForge** is a lightweight, high-performance Dapper-based data access layer that provides an **Entity Framework-style API surface** while maintaining Dapper's speed and simplicity. It's designed for applications that need maximum query performance with minimal overhead.

## Key Philosophy

- **Immediate execution** - No change tracker, no `SaveChanges()`
- **Explicit control** - Direct SQL execution for maximum performance
- **Minimal allocations** - Reduced memory pressure compared to EF Core
- **Database-agnostic core** - Pluggable SQL dialects
- **Fluent API** - Familiar Entity Framework-like query and configuration syntax

## NuGet Packages

- **Nahmadov.DapperForge.Core** - Core library with abstractions
- **Nahmadov.DapperForge.SqlServer** - SQL Server dialect implementation
- **Nahmadov.DapperForge.Oracle** - Oracle dialect implementation

## Quick Start

### Installation

```bash
dotnet add package Nahmadov.DapperForge.Core
dotnet add package Nahmadov.DapperForge.SqlServer
```

### Define Your Context

```csharp
public class AppDbContext : DapperDbContext
{
    public DapperSet<Customer> Customers => Set<Customer>();
    public DapperSet<Order> Orders => Set<Order>();

    public AppDbContext(DapperDbContextOptions<AppDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(DapperModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(b =>
        {
            b.ToTable("Customers", "dbo");
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).HasMaxLength(100).IsRequired();
        });
    }
}
```

### Register in DI

```csharp
services.AddDapperDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"));
});
```

### Use in Your Application

```csharp
public class CustomerService
{
    private readonly AppDbContext _db;

    public CustomerService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Customer>> GetActiveCustomersAsync()
    {
        return await _db.Customers
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task CreateCustomerAsync(Customer customer)
    {
        await _db.Customers.InsertAsync(customer);
    }
}
```

## Key Features

### 1. Fluent Query API
```csharp
var activeUsers = await db.Users
    .Where(u => u.IsActive)
    .OrderBy(u => u.Name)
    .Skip(10)
    .Take(20)
    .ToListAsync();
```

### 2. Expression Translation
LINQ expressions are translated to parameterized SQL without loading all data into memory.

```csharp
var users = await db.Users.WhereAsync(u =>
    u.Name.StartsWith("John") && u.Age > 18);
// Generates: WHERE a.[Name] LIKE @p0 AND a.[Age] > @Age
```

### 3. Include/ThenInclude Support
```csharp
var orders = await db.Orders
    .Include(o => o.Customer)
        .ThenInclude(c => c.Address)
    .Include(o => o.OrderItems)
        .ThenInclude(i => i.Product)
    .AsSplitQuery()
    .ToListAsync();
```

### 4. Automatic Retry Logic
Automatic exponential backoff retry for transient database failures.

### 5. Transaction Scopes
```csharp
var scope = await db.BeginTransactionScopeAsync();
try
{
    await db.Users.InsertAsync(user, scope.Transaction);
    await db.Orders.InsertAsync(order, scope.Transaction);
    scope.Complete();
}
finally
{
    scope.Dispose(); // Auto-commits or rolls back
}
```

### 6. Validation Before Mutations
```csharp
modelBuilder.Entity<Customer>(b =>
{
    b.Property(c => c.Email).IsRequired().HasMaxLength(100);
});

// Throws DapperValidationException if validation fails
await db.Customers.InsertAsync(customer);
```

### 7. Database Dialect Abstraction
```csharp
// SQL Server
options.UseSqlServer(connectionString);

// Oracle
options.UseOracle(connectionString);
```

## Performance Characteristics

### Strengths
- Minimal memory allocations (no change tracker)
- Direct SQL execution (no query plan overhead)
- Efficient expression compilation caching
- Connection pooling and reuse
- Parameterized queries enable database query plan caching

### Trade-offs vs EF Core
- ✅ Better performance for simple CRUD operations
- ✅ Simpler mental model (immediate execution)
- ✅ Less overhead for read-heavy workloads
- ❌ Limited LINQ support (no aggregations, grouping in fluent API)
- ❌ Must use raw SQL for complex queries
- ❌ No lazy loading (must use Include)
- ❌ No change tracking
- ❌ No migrations

## When to Use DapperForge

### ✅ Good Fit
- High-performance CRUD applications
- Microservices with simple data access patterns
- Read-heavy workloads
- APIs with predictable query patterns
- Applications that need explicit control over SQL

### ❌ Not Ideal
- Complex domain models with many relationships
- Applications requiring lazy loading
- Heavy use of complex LINQ queries
- Need for automatic change tracking
- Projects requiring database migrations

## Project Structure

```
DapperToolkit/
├── src/
│   ├── Nahmadov.DapperForge.Core/       # Core library
│   ├── Nahmadov.DapperForge.SqlServer/  # SQL Server dialect
│   └── Nahmadov.DapperForge.Oracle/     # Oracle dialect
├── tests/
│   └── Nahmadov.DapperForge.UnitTests/  # Unit tests
└── samples/
    └── ConnectionSample/                # Usage samples
```

## Next Steps

- [Architecture Documentation](./DapperForge-Architecture.md) - Detailed architecture overview
- [Usage Guide](./DapperForge-Usage-Guide.md) - Comprehensive usage examples
- [Complete Reference](./DapperForge-Complete-Reference.md) - Full API reference

## License

Check the repository for license information.
