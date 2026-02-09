# Nahmadov.DapperForge

A lightweight, high-performance Dapper-based data access layer that provides an **Entity Framework-style API surface** while maintaining Dapper's speed and simplicity.

---

## Key Philosophy

- **Immediate execution** — No change tracker, no `SaveChanges()`
- **Explicit control** — Direct SQL execution for maximum performance
- **Minimal allocations** — Reduced memory pressure compared to EF Core
- **Database-agnostic** — Pluggable SQL dialects (SQL Server, Oracle)
- **Fluent API** — Familiar EF-like query and configuration syntax

---

## NuGet Packages

| Package | Description |
|---------|-------------|
| `Nahmadov.DapperForge.Core` | Core library with abstractions |
| `Nahmadov.DapperForge.SqlServer` | SQL Server dialect |
| `Nahmadov.DapperForge.Oracle` | Oracle dialect |

---

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
var activeCustomers = await _db.Customers
    .Where(c => c.IsActive)
    .OrderBy(c => c.Name)
    .ToListAsync();

var id = await _db.Customers.InsertAndGetIdAsync<int>(new Customer
{
    Name = "Alice",
    IsActive = true
});

await _db.Customers.DeleteByIdAsync(id);
```

---

## Features

### Fluent Query API

```csharp
var customers = await db.Customers
    .Where(c => c.IsActive && c.Name.StartsWith("John"))
    .OrderBy(c => c.Name)
    .Skip(10)
    .Take(20)
    .ToListAsync();
```

### Expression Translation

LINQ expressions are translated to parameterized SQL. Supported patterns:

| Pattern | Example | Generated SQL |
|---------|---------|---------------|
| Comparisons | `c.Age > 18` | `WHERE a.[Age] > @Age` |
| Null checks | `c.Email == null` | `WHERE a.[Email] IS NULL` |
| Booleans | `c.IsActive` / `!c.IsActive` | `WHERE a.[IsActive] = 1` / `= 0` |
| String methods | `c.Name.StartsWith("J")` | `WHERE a.[Name] LIKE @p0` |
| Collection IN | `ids.Contains(c.Id)` | `WHERE a.[Id] IN (1,2,3)` |
| Logical ops | `&&`, `\|\|`, `!` | `AND`, `OR`, `NOT` |

Case-insensitive filtering is available via the `ignoreCase` parameter:

```csharp
var customers = await db.Customers.WhereAsync(
    c => c.Name.Contains("john"), ignoreCase: true);
// WHERE LOWER(a.[Name]) LIKE LOWER(@p0)
```

### Include / ThenInclude

Load related entities with an EF-style API:

```csharp
var orders = await db.Orders
    .Include(o => o.Customer)
        .ThenInclude(c => c.Address)
    .Include(o => o.OrderItems)
        .ThenInclude(i => i.Product)
    .AsSplitQuery()
    .ToListAsync();
```

**AsSingleQuery** (default) — single SQL with JOINs; best for small result sets.
**AsSplitQuery** — separate queries with `IN` clauses; avoids cartesian explosion for collections.

### Bulk Insert

Insert large collections efficiently using automatic batching. Entities are split into batches that respect database parameter limits (2,100 for SQL Server, 32,767 for Oracle).

```csharp
var products = GenerateProducts(5000);

var result = await db.Products.BulkInsertAsync(products);

Console.WriteLine($"Inserted {result.TotalAffected} rows in {result.BatchCount} batch(es), took {result.ElapsedTime}");
```

Configure with `BulkInsertOptions`:

```csharp
var result = await db.Products.BulkInsertAsync(products, new BulkInsertOptions
{
    BatchSize = 200,
    CommandTimeout = 60,
    ValidateBeforeInsert = true   // default: true
});
```

Works with transactions:

```csharp
var scope = await db.BeginTransactionScopeAsync();
try
{
    await db.Products.BulkInsertAsync(products, transaction: scope.Transaction);
    scope.Complete();
}
finally
{
    scope.Dispose();
}
```

### Bulk Merge (Upsert)

Perform INSERT-or-UPDATE (MERGE) operations in bulk. Uses the SQL `MERGE` statement under the hood.

```csharp
var result = await db.Products.BulkMergeAsync(products);

Console.WriteLine($"Affected {result.TotalAffected} rows (inserted: {result.RowsInserted}, updated: {result.RowsUpdated})");
```

Control merge behavior with `BulkMergeOptions`:

```csharp
var result = await db.Products.BulkMergeAsync(products, new BulkMergeOptions
{
    MatchColumns = new[] { "TenantId", "ProductCode" },  // columns used for matching
    Mode = MergeMode.InsertOrUpdate,  // or InsertOnly, UpdateOnly
    BatchSize = 100,
    ValidateBeforeMerge = true
});
```

Shorthand for custom match columns:

```csharp
var result = await db.Products.BulkMergeAsync(
    products,
    matchColumns: new[] { "TenantId", "ProductCode" });
```

**Merge Modes:**

| Mode | Behavior |
|------|----------|
| `InsertOrUpdate` | Insert new rows, update existing (default) |
| `InsertOnly` | Only insert rows that don't exist |
| `UpdateOnly` | Only update existing rows |

### Transactions

```csharp
var scope = await db.BeginTransactionScopeAsync();
try
{
    var customerId = await db.Customers.InsertAndGetIdAsync<int>(customer, scope.Transaction);
    order.CustomerId = customerId;
    await db.Orders.InsertAsync(order, scope.Transaction);
    scope.Complete();
}
finally
{
    scope.Dispose();
}
```

### Validation

Entities are validated before insert/update using `[Required]`, `[MaxLength]`, `[StringLength]` attributes or fluent configuration.

```csharp
modelBuilder.Entity<Customer>(b =>
{
    b.Property(c => c.Name).IsRequired().HasMaxLength(100);
});

// Throws DapperValidationException on invalid data
await db.Customers.InsertAsync(customer);
```

Read-only entities and properties are protected from mutation.

### Automatic Retry

Transient database failures (timeouts, deadlocks, connection drops) are automatically retried with exponential backoff.

---

## Entity Configuration

### Fluent API

```csharp
public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers", "dbo");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasColumnName("FullName").HasMaxLength(100).IsRequired();
        builder.Property(c => c.CreatedAt).IsReadOnly();
    }
}
```

Apply configurations:

```csharp
protected override void OnModelCreating(DapperModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
}
```

### Data Annotations

```csharp
[Table("Customers", Schema = "dbo")]
public class Customer
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100), Column("FullName")]
    public string Name { get; set; }

    [ReadOnly]
    public DateTime CreatedAt { get; set; }
}
```

### Key Detection

Keys are resolved in order: `[Key]` attribute, fluent `HasKey()`, property named `Id`, or property named `{TypeName}Id`.

---

## Database Dialects

| Feature | SQL Server | Oracle |
|---------|-----------|--------|
| Identifier quoting | `[Table]` | `"Table"` |
| Parameter prefix | `@p0` | `:p0` |
| Auto-generated keys | `SCOPE_IDENTITY()` | `RETURNING ... INTO` |
| Max parameters | 2,100 | 32,767 |
| Default schema | `dbo` | _(none)_ |

```csharp
// SQL Server
options.UseSqlServer(connectionString);

// Oracle
options.UseOracle(connectionString);
```

---

## CRUD API Reference

| Operation | Method |
|-----------|--------|
| Get all | `GetAllAsync()` |
| Find by ID | `FindAsync(id)` |
| Filter | `WhereAsync(predicate)` / `.Where(pred).ToListAsync()` |
| First | `FirstOrDefaultAsync(predicate)` |
| Any | `AnyAsync(predicate)` |
| Count | `CountAsync(predicate)` |
| Insert | `InsertAsync(entity)` |
| Insert + get ID | `InsertAndGetIdAsync<TKey>(entity)` |
| Update | `UpdateAsync(entity)` |
| Delete | `DeleteAsync(entity)` / `DeleteByIdAsync(id)` |
| Bulk insert | `BulkInsertAsync(entities, options?)` |
| Bulk merge | `BulkMergeAsync(entities, options?)` |

---

## Architecture

```
Application Layer
       │
   DapperDbContext  (connection management, entity sets)
       │
  ┌────┼────┐
Query  Mutation  Config
  │    │         │
  SQL Generation Layer  (SqlGenerator, PredicateVisitor)
       │
  SQL Dialect Layer  (SqlServer, Oracle)
       │
  Dapper + ADO.NET
```

**Design patterns:** Repository (`DapperSet<T>`), Unit of Work (`TransactionScope`), Builder (model configuration), Strategy (`ISqlDialect`), Facade, Factory, Template Method.

---

## Performance

- No change tracker — minimal memory overhead
- SQL statements pre-generated and cached
- Expression compilation cached (thread-safe LRU, max 1,000 entries)
- Identity cache prevents duplicate instances during Include operations
- Parameterized queries enable database query plan caching
- Bulk operations use automatic batching for optimal throughput

---

## Migration from EF Core

| EF Core | DapperForge |
|---------|-------------|
| `DbContext` | `DapperDbContext` |
| `DbSet<T>` | `DapperSet<T>` |
| `Where(pred).ToListAsync()` | `WhereAsync(pred)` or `.Where(pred).ToListAsync()` |
| `Add() + SaveChangesAsync()` | `InsertAsync()` |
| `Update() + SaveChangesAsync()` | `UpdateAsync()` |
| `Remove() + SaveChangesAsync()` | `DeleteAsync()` |
| `Include()` / `ThenInclude()` | Same API |
| `AsNoTracking()` | N/A (never tracks) |
| `SaveChangesAsync()` | N/A (immediate execution) |

---

## Limitations

1. Single-column keys only — no composite key support
2. Limited LINQ — no aggregations, grouping, or complex joins in the fluent API (use raw SQL)
3. No lazy loading — must use `Include`
4. No change tracking
5. No migrations — DDL managed externally
6. Expression translator covers common filter patterns only

---

## Project Structure

```
DapperToolkit/
├── src/
│   ├── Nahmadov.DapperForge.Core/       # Core library
│   ├── Nahmadov.DapperForge.SqlServer/  # SQL Server dialect
│   └── Nahmadov.DapperForge.Oracle/     # Oracle dialect
├── tests/
│   └── Nahmadov.DapperForge.UnitTests/  # Unit tests
├── samples/
│   └── ConnectionSample/                # Usage samples
└── docs/                                # Detailed documentation
```

## Testing

```bash
dotnet test
```

## Sample App

```bash
cd samples/ConnectionSample
dotnet run
```

Configure via user secrets (`FullSample`):
- `Provider`: `SqlServer` (default) or `Oracle`
- `SqlServerConnectionString` / `OracleConnectionString`

---

## Documentation

- [Overview](docs/DapperForge-Overview.md)
- [Architecture](docs/DapperForge-Architecture.md)
- [Usage Guide](docs/DapperForge-Usage-Guide.md)
- [Complete Reference](docs/DapperForge-Complete-Reference.md)
