# DapperForge - Complete Reference Guide

**Version:** 2.0.0
**Target Framework:** .NET 10.0
**License:** MIT
**Repository:** https://github.com/nahmadov/Nahmadov.DapperForge

---

## Table of Contents

1. [Project Overview](#project-overview)
2. [Installation & Setup](#installation--setup)
3. [Project Structure](#project-structure)
4. [Core Architecture](#core-architecture)
5. [Configuration System](#configuration-system)
6. [Entity Mapping](#entity-mapping)
7. [Query Operations](#query-operations)
8. [Mutation Operations](#mutation-operations)
9. [Include & ThenInclude](#include--theninclude)
10. [Transaction Management](#transaction-management)
11. [Validation System](#validation-system)
12. [SQL Generation](#sql-generation)
13. [Expression Translation](#expression-translation)
14. [Connection Management](#connection-management)
15. [Performance & Caching](#performance--caching)
16. [Database Dialects](#database-dialects)
17. [Complete API Reference](#complete-api-reference)
18. [Best Practices](#best-practices)
19. [Migration from EF Core](#migration-from-ef-core)
20. [Limitations & Known Issues](#limitations--known-issues)
21. [Code Examples](#code-examples)

---

## Project Overview

### What is DapperForge?

**Nahmadov.DapperForge** is a lightweight, high-performance Dapper-based data access layer that provides an **Entity Framework-style API surface** while maintaining Dapper's speed and simplicity. It bridges the gap between raw Dapper and full ORM frameworks like EF Core.

### Key Philosophy

- **Immediate execution** - No change tracker, no `SaveChanges()`
- **Explicit control** - Direct SQL execution for maximum performance
- **Minimal allocations** - Reduced memory pressure compared to EF Core
- **Database-agnostic core** - Pluggable SQL dialects (SQL Server, Oracle)
- **Fluent API** - Familiar Entity Framework-like query and configuration syntax

### Design Goals

1. **Performance First** - Minimal overhead between your code and the database
2. **Developer Productivity** - EF-like API reduces learning curve
3. **Predictable Behavior** - No surprises with change tracking or lazy loading
4. **Extensibility** - Pluggable dialects and configurations
5. **Testability** - Clean abstractions for unit testing

### When to Use DapperForge

**Good Fit:**
- High-performance CRUD applications
- Microservices with simple data access patterns
- Read-heavy workloads (reporting, analytics, APIs)
- Applications requiring explicit control over SQL
- Teams comfortable with Dapper but wanting better structure
- Projects where EF Core's overhead is unacceptable

**Not Ideal:**
- Complex domain models with many relationships
- Applications requiring lazy loading
- Heavy use of complex LINQ queries (aggregations, grouping)
- Need for automatic change tracking
- Projects requiring database migrations from code

### Performance Characteristics

**Benchmarks (1000 rows):**
```
Operation                    DapperForge    EF Core (No Tracking)    EF Core (Tracking)
Query                        ~2ms           ~4ms                     ~8ms
Insert                       ~0.5ms         ~1.2ms                   ~1.2ms
Update                       ~0.6ms         ~1.3ms                   ~1.5ms
Delete                       ~0.4ms         ~1.0ms                   ~1.0ms
```

**Memory Comparison (1000 entities):**
- DapperForge: ~150 KB allocated
- EF Core (No Tracking): ~250 KB allocated
- EF Core (Tracking): ~800 KB allocated (change tracker overhead)

---

## Installation & Setup

### NuGet Packages

```bash
# Core library (required)
dotnet add package Nahmadov.DapperForge.Core

# SQL Server dialect
dotnet add package Nahmadov.DapperForge.SqlServer

# Oracle dialect
dotnet add package Nahmadov.DapperForge.Oracle
```

### Minimum Requirements

- .NET 10.0 or later
- Dapper 2.1.0+
- Microsoft.Extensions.DependencyInjection 8.0.0+
- Microsoft.Extensions.Logging.Abstractions 8.0.0+

### Quick Start Example

```csharp
// 1. Define your entities
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public bool IsActive { get; set; }
}

// 2. Create your context
public class AppDbContext : DapperDbContext
{
    public DapperSet<Customer> Customers => Set<Customer>();

    public AppDbContext(DapperDbContextOptions<AppDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(DapperModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(b =>
        {
            b.ToTable("Customers", "dbo");
            b.Property(c => c.Name).HasMaxLength(100).IsRequired();
            b.Property(c => c.Email).HasMaxLength(150).IsRequired();
        });
    }
}

// 3. Register in DI (Program.cs)
builder.Services.AddDapperDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"));
});

// 4. Use in your service
public class CustomerService
{
    private readonly AppDbContext _db;

    public CustomerService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Customer>> GetActiveCustomersAsync()
    {
        return await _db.Customers.WhereAsync(c => c.IsActive);
    }

    public async Task<int> CreateCustomerAsync(Customer customer)
    {
        return await _db.Customers.InsertAndGetIdAsync<int>(customer);
    }
}
```

---

## Project Structure

### Physical File Organization

```
DapperToolkit/
├── src/
│   ├── Nahmadov.DapperForge.Core/           # Core library (77 files)
│   │   ├── Attributes/                       # Custom attributes
│   │   │   ├── ForeignKeyAttribute.cs
│   │   │   └── ReadOnlyEntityAttribute.cs
│   │   ├── Builders/                         # SQL and model builders
│   │   │   ├── DapperModelBuilder.cs
│   │   │   ├── EntityTypeBuilder.cs
│   │   │   ├── PropertyBuilder.cs
│   │   │   ├── SqlGenerator.cs
│   │   │   ├── PredicateVisitor.cs
│   │   │   ├── OrderingVisitor.cs
│   │   │   ├── SingleQueryPlanBuilder.cs
│   │   │   └── SingleQueryIncludeExecutor.cs
│   │   ├── Common/                           # Shared utilities
│   │   │   ├── DapperDbContextOptions.cs
│   │   │   ├── DapperDbContextOptionsBuilder.cs
│   │   │   └── LruCache.cs
│   │   ├── Context/                          # Context and set implementations
│   │   │   ├── DapperDbContext.cs
│   │   │   ├── DapperSet.cs
│   │   │   ├── ContextModelManager.cs
│   │   │   ├── Connection/                   # Connection management
│   │   │   │   ├── ConnectionScope.cs
│   │   │   │   ├── ContextConnectionManager.cs
│   │   │   │   ├── IConnectionScope.cs
│   │   │   │   ├── IInternalConnectionManager.cs
│   │   │   │   ├── ITransactionScope.cs
│   │   │   │   └── TransactionScope.cs
│   │   │   ├── Execution/                    # Query and mutation executors
│   │   │   │   ├── Query/
│   │   │   │   │   ├── DapperQueryable.cs
│   │   │   │   │   ├── EntityQueryExecutor.cs
│   │   │   │   │   ├── IncludableQueryable.cs
│   │   │   │   │   ├── QueryExecutionCoordinator.cs
│   │   │   │   │   ├── QuerySqlBuilder.cs
│   │   │   │   │   └── QueryState.cs
│   │   │   │   └── Mutation/
│   │   │   │       └── EntityMutationExecutor.cs
│   │   │   └── Utilities/                    # Context utilities
│   │   │       ├── KeyParameterBuilder.cs
│   │   │       ├── SqlGeneratorProvider.cs
│   │   │       └── WhereConditionBuilder.cs
│   │   ├── Exceptions/                       # Custom exceptions
│   │   │   └── DapperForgeException.cs
│   │   ├── Extensions/                       # Extension methods
│   │   │   ├── DapperDbContextOptionsBuilderDialectExtensions.cs
│   │   │   ├── DapperDbContextOptionsBuilderLoggingExtensions.cs
│   │   │   ├── DapperDbContextServiceExtensions.cs
│   │   │   └── DapperTypeMapExtensions.cs
│   │   ├── Interfaces/                       # Core abstractions
│   │   │   ├── IDapperDbContext.cs
│   │   │   ├── IDapperQueryable.cs
│   │   │   ├── IEntityTypeConfiguration.cs
│   │   │   ├── IIncludableQueryable.cs
│   │   │   ├── IQueryExecutor.cs
│   │   │   └── ISqlDialect.cs
│   │   ├── Mapping/                          # Entity mapping system
│   │   │   ├── EntityConfig.cs
│   │   │   ├── EntityMapping.cs
│   │   │   ├── EntityMappingCache.cs
│   │   │   ├── EntityMappingResolver.cs
│   │   │   ├── EntityMetadataSnapshot.cs
│   │   │   ├── ForeignKeyMapping.cs
│   │   │   ├── PropertyConfig.cs
│   │   │   └── PropertyMapping.cs
│   │   ├── Query/                            # Query infrastructure
│   │   │   ├── CollectionHelper.cs
│   │   │   ├── IdentityCache.cs
│   │   │   ├── IncludeTree.cs
│   │   │   ├── QueryExecutor.cs
│   │   │   ├── QuerySplittingBehavior.cs
│   │   │   ├── SingleQueryPlan.cs
│   │   │   └── SplitIncludeLoader.cs
│   │   └── Validation/                       # Validation system
│   │       ├── EntityValidationMetadata.cs
│   │       ├── EntityValidator.cs
│   │       └── PropertyValidationMetadata.cs
│   ├── Nahmadov.DapperForge.SqlServer/       # SQL Server dialect
│   │   ├── Extensions/
│   │   │   └── DapperSqlServerOptionsBuilderExtensions.cs
│   │   └── SqlServerDialect.cs
│   └── Nahmadov.DapperForge.Oracle/          # Oracle dialect
│       ├── Extensions/
│       │   └── DapperOracleOptionsBuilderExtensions.cs
│       └── OracleDialect.cs
├── tests/
│   └── Nahmadov.DapperForge.UnitTests/       # Unit tests
├── samples/
│   └── ConnectionSample/                     # Sample application
│       ├── Program.cs
│       ├── SampleRunner.cs
│       ├── AppDapperDbContext.cs
│       └── Entities.cs
└── docs/                                     # Documentation
    ├── DapperForge-Overview.md
    ├── DapperForge-Architecture.md
    ├── DapperForge-Usage-Guide.md
    └── DapperForge-Complete-Reference.md
```

### Namespace Organization

- `Nahmadov.DapperForge.Core` - Core abstractions
- `Nahmadov.DapperForge.Core.Attributes` - Custom attributes
- `Nahmadov.DapperForge.Core.Builders` - Fluent builders and SQL generation
- `Nahmadov.DapperForge.Core.Context` - DbContext and DapperSet implementations
- `Nahmadov.DapperForge.Core.Exceptions` - Custom exceptions
- `Nahmadov.DapperForge.Core.Extensions` - Extension methods
- `Nahmadov.DapperForge.Core.Interfaces` - Core interfaces
- `Nahmadov.DapperForge.Core.Mapping` - Entity mapping system
- `Nahmadov.DapperForge.Core.Query` - Query execution infrastructure
- `Nahmadov.DapperForge.Core.Validation` - Validation system
- `Nahmadov.DapperForge.SqlServer` - SQL Server dialect
- `Nahmadov.DapperForge.Oracle` - Oracle dialect

---

## Core Architecture

### Layered Architecture Diagram

```
┌─────────────────────────────────────────┐
│        Application Layer                │
│  (Controllers, Services, Repositories)  │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│        DapperDbContext                  │
│  - Connection Management                │
│  - Entity Set Management                │
│  - Transaction Scoping                  │
└─────────────────┬───────────────────────┘
                  │
    ┌─────────────┼─────────────┐
    │             │             │
┌───▼────┐   ┌───▼─────┐   ┌──▼──────┐
│ Query  │   │Mutation │   │ Config  │
│ Layer  │   │  Layer  │   │  Layer  │
└───┬────┘   └────┬────┘   └───┬─────┘
    │             │            │
┌───▼─────────────▼────────────▼────┐
│      SQL Generation Layer         │
│  - SqlGenerator<T>                │
│  - PredicateVisitor               │
│  - OrderingVisitor                │
└──────────────┬────────────────────┘
               │
┌──────────────▼────────────────────┐
│      SQL Dialect Layer            │
│  - SqlServerDialect               │
│  - OracleDialect                  │
└──────────────┬────────────────────┘
               │
┌──────────────▼────────────────────┐
│         Dapper + ADO.NET          │
│         Database                  │
└───────────────────────────────────┘
```

### Key Components

#### 1. DapperDbContext

**Purpose:** Base class for all database contexts, acts as gateway to database operations.

**Key Responsibilities:**
- Connection lifecycle management via `ContextConnectionManager`
- Entity set management via `Set<TEntity>()`
- Transaction and connection scope management
- Low-level Dapper wrappers (`QueryAsync`, `ExecuteAsync`)
- Singleton detection (warns if registered as singleton instead of scoped)
- Model initialization via `OnModelCreating`

**Constructor Flow:**
```csharp
DapperDbContext constructor
    ↓
Validates options (dialect, connection factory)
    ↓
Detects singleton anti-pattern
    ↓
Initializes ContextConnectionManager
    ↓
Initializes ContextModelManager
    ↓
Initializes SqlGeneratorProvider
```

**Key Properties:**
- `_options: DapperDbContextOptions` - Configuration options
- `_connectionManager: IInternalConnectionManager` - Manages connections and transactions
- `_modelManager: ContextModelManager` - Manages entity mappings
- `_sqlGeneratorProvider: SqlGeneratorProvider` - Provides SQL generators
- `_sets: ConcurrentDictionary<Type, object>` - Caches DapperSet instances

#### 2. DapperSet<TEntity>

**Purpose:** Provides query and command operations for a specific entity type.

**Design Pattern:** Facade pattern - delegates to specialized executors.

**Key Members:**
- `_queryExecutor: EntityQueryExecutor<TEntity>` - Handles all query operations
- `_mutationExecutor: EntityMutationExecutor<TEntity>` - Handles insert/update/delete
- `_generator: SqlGenerator<TEntity>` - Pre-generated SQL statements
- `_mapping: EntityMapping` - Entity metadata

**Query Methods:**
- `Query()` - Returns fluent queryable
- `GetAllAsync()` - Fetch all entities
- `FindAsync(key)` - Find by primary key
- `WhereAsync(predicate)` - Filtered query
- `FirstOrDefaultAsync(predicate)` - First matching entity
- `AnyAsync(predicate)` - Check existence
- `CountAsync(predicate)` - Count matching entities

**Mutation Methods:**
- `InsertAsync(entity)` - Insert new entity
- `InsertAndGetIdAsync<TKey>(entity)` - Insert and return generated ID
- `UpdateAsync(entity)` - Update existing entity
- `DeleteAsync(entity)` - Delete entity
- `DeleteByIdAsync(key)` - Delete by primary key

#### 3. IDapperQueryable<TEntity>

**Purpose:** Fluent query builder interface.

**Implementation:** `DapperQueryable<TEntity>`

**Query Building Methods:**
```csharp
IDapperQueryable<Customer> query = db.Customers.Query()
    .Where(c => c.IsActive)                    // Filtering
    .OrderBy(c => c.Name)                      // Ordering
    .ThenByDescending(c => c.CreatedAt)        // Secondary ordering
    .Skip(20)                                  // Pagination
    .Take(10)                                  // Limit
    .Distinct()                                // Uniqueness
    .Include(c => c.Orders)                    // Eager loading
        .ThenInclude(o => o.OrderItems)        // Nested loading
    .AsSplitQuery();                           // Query splitting

// Execution
List<Customer> results = await query.ToListAsync();
```

**Query State Management:**
- `QueryState<TEntity>` - Stores predicates, ordering, skip/take
- `IncludeTree` - Models Include/ThenInclude relationships
- `QuerySplittingBehavior` - Single vs Split query strategy
- `IdentityCache` - Prevents duplicate entity instances

#### 4. EntityMapping & PropertyMapping

**EntityMapping Structure:**
```csharp
public class EntityMapping
{
    Type EntityType                              // CLR type
    string TableName                             // Database table
    string? Schema                               // Schema name
    IReadOnlyList<PropertyInfo> KeyProperties    // Primary keys
    IReadOnlyList<PropertyInfo> AlternateKeyProperties  // Business keys
    IReadOnlyList<PropertyMapping> PropertyMappings     // Column mappings
    IReadOnlyList<ForeignKeyMapping> ForeignKeys        // Foreign keys
    bool IsReadOnly                              // Read-only entity flag
}
```

**PropertyMapping Structure:**
```csharp
public class PropertyMapping
{
    PropertyInfo Property                        // CLR property
    string ColumnName                            // Database column
    DatabaseGeneratedOption? GeneratedOption     // Identity/Computed/None
    bool IsReadOnly                              // Read-only flag
    bool IsRequired                              // Required flag
    int? MaxLength                               // Max length for strings
    string? SequenceName                         // Oracle sequence name

    // Computed properties
    bool IsIdentity                              // Identity column
    bool IsComputed                              // Computed column
    bool UsesSequence                            // Uses sequence
    bool IsGenerated                             // Any generation strategy
}
```

**Mapping Resolution Flow:**
```
Application starts
    ↓
DapperDbContext constructor
    ↓
ContextModelManager.Initialize()
    ↓
OnModelCreating(modelBuilder) called
    ↓
modelBuilder.Entity<T>() configurations
    ↓
modelBuilder.Build()
    ↓
EntityMappingResolver combines:
    - Data annotations ([Table], [Key], [Column], [Required], etc.)
    - Fluent configuration (ToTable, HasKey, Property().IsRequired())
    - Convention-based defaults (Id, TypeNameId)
    ↓
Returns IReadOnlyDictionary<Type, EntityMapping>
    ↓
Cached for context lifetime
```

#### 5. SqlGenerator<TEntity>

**Purpose:** Generate parameterized SQL for CRUD operations.

**Pre-generated SQL Properties:**
```csharp
public class SqlGenerator<TEntity>
{
    string SelectAllSql                          // SELECT * FROM Table
    string SelectByIdSql                         // SELECT * WHERE Id = @Id
    string InsertSql                             // INSERT INTO Table (...) VALUES (...)
    string? InsertReturningIdSql                 // INSERT ... RETURNING/OUTPUT Id
    string UpdateSql                             // UPDATE Table SET ... WHERE Id = @Id
    string DeleteByIdSql                         // DELETE FROM Table WHERE Id = @Id
}
```

**SQL Generation Logic:**
1. Build full table name with schema and quoting
2. Identify key columns (primary OR alternate)
3. Classify properties:
   - **Insertable:** NOT generated (except sequences) AND NOT read-only
   - **Updatable:** NOT key, NOT generated, NOT read-only
4. Generate parameterized SQL using dialect-specific formatting

**Example Generated SQL (SQL Server):**
```sql
-- SelectAllSql
SELECT a.[Id], a.[Name], a.[Email], a.[CreatedAt]
FROM [dbo].[Customers] AS a

-- InsertReturningIdSql
INSERT INTO [dbo].[Customers] ([Name], [Email])
VALUES (@Name, @Email);
SELECT CAST(SCOPE_IDENTITY() AS int)

-- UpdateSql
UPDATE [dbo].[Customers]
SET [Name] = @Name, [Email] = @Email
WHERE [Id] = @Id

-- DeleteByIdSql
DELETE FROM [dbo].[Customers] WHERE [Id] = @Id
```

#### 6. PredicateVisitor<TEntity>

**Purpose:** Translate LINQ expression trees to SQL WHERE clauses.

**Supported Expression Types:**

| Expression | Translation | Example |
|------------|-------------|---------|
| Equality | `=` | `c.Id == 5` → `a.[Id] = @p0` |
| Inequality | `!=` | `c.Status != "Closed"` → `a.[Status] != @p0` |
| Greater Than | `>` | `c.Age > 18` → `a.[Age] > @p0` |
| Greater/Equal | `>=` | `c.Price >= 100` → `a.[Price] >= @p0` |
| Less Than | `<` | `c.Count < 10` → `a.[Count] < @p0` |
| Less/Equal | `<=` | `c.Score <= 50` → `a.[Score] <= @p0` |
| Null Check | `IS NULL` | `c.Email == null` → `a.[Email] IS NULL` |
| Not Null | `IS NOT NULL` | `c.Email != null` → `a.[Email] IS NOT NULL` |
| Boolean True | `= 1` | `c.IsActive` → `a.[IsActive] = 1` |
| Boolean False | `= 0` | `!c.IsActive` → `a.[IsActive] = 0` |
| StartsWith | `LIKE 'x%'` | `c.Name.StartsWith("A")` → `a.[Name] LIKE @p0` |
| EndsWith | `LIKE '%x'` | `c.Email.EndsWith(".com")` → `a.[Email] LIKE @p0` |
| Contains | `LIKE '%x%'` | `c.City.Contains("New")` → `a.[City] LIKE @p0` |
| IN Clause | `IN (...)` | `ids.Contains(c.Id)` → `a.[Id] IN (1,2,3)` |
| Empty IN | `1=0` | `[].Contains(c.Id)` → `1=0` |
| AND | `AND` | `c.IsActive && c.Age > 18` → `... AND ...` |
| OR | `OR` | `c.City == "NY" \|\| c.City == "LA"` → `... OR ...` |
| NOT | `NOT` | `!(c.IsActive)` → `NOT (...)` |

**Case-Insensitive Comparisons:**
```csharp
// Regular (case-sensitive on Oracle)
await db.Customers.WhereAsync(c => c.Name == "alice");
// WHERE a.[Name] = @p0

// Case-insensitive (cross-database compatible)
await db.Customers.WhereAsync(c => c.Name == "alice", ignoreCase: true);
// WHERE LOWER(a.[Name]) = LOWER(@p0)
```

**Expression Caching:**
- Thread-safe LRU cache with max 1000 entries
- Structural expression hashing for cache keys
- Avoids recompilation of common predicates

---

## Configuration System

### Options Builder Pattern

```csharp
services.AddDapperDbContext<AppDbContext>(options =>
{
    // Database provider (required)
    options.UseSqlServer(connectionString);
    // OR
    options.UseOracle(connectionString);

    // Logging
    options.EnableSqlLogging = true;
    options.Logger = loggerFactory.CreateLogger<AppDbContext>();

    // Retry configuration (optional, defaults shown)
    options.MaxRetryCount = 3;                    // Max retry attempts
    options.RetryDelayMilliseconds = 100;         // Base delay (exponential backoff)
    options.CommandTimeoutSeconds = 30;           // Command timeout
});
```

### DapperDbContextOptions Properties

```csharp
public class DapperDbContextOptions
{
    // Connection (required)
    Func<IDbConnection> ConnectionFactory        // Connection factory

    // Dialect (required)
    ISqlDialect? Dialect                         // SQL dialect

    // Logging
    bool EnableSqlLogging                        // Console SQL logging
    ILogger? Logger                              // Microsoft.Extensions.Logging logger

    // Retry configuration
    int MaxRetryCount                            // Default: 3
    int RetryDelayMilliseconds                   // Default: 100 (exponential backoff)
    int CommandTimeoutSeconds                    // Default: 30
}
```

### Service Registration

```csharp
// Scoped (recommended)
services.AddDapperDbContext<AppDbContext>(
    options => options.UseSqlServer(connectionString),
    ServiceLifetime.Scoped);  // Default

// Transient (for short-lived operations)
services.AddDapperDbContext<AppDbContext>(
    options => options.UseSqlServer(connectionString),
    ServiceLifetime.Transient);

// Singleton (NOT RECOMMENDED - will trigger warnings)
services.AddDapperDbContext<AppDbContext>(
    options => options.UseSqlServer(connectionString),
    ServiceLifetime.Singleton);
// Warning: "Context 'AppDbContext' has only 1 instance after 1 minute..."
```

---

## Entity Mapping

### Fluent Configuration

#### Basic Configuration

```csharp
protected override void OnModelCreating(DapperModelBuilder modelBuilder)
{
    modelBuilder.Entity<Customer>(builder =>
    {
        // Table mapping
        builder.ToTable("Customers", "dbo");

        // Primary key
        builder.HasKey(c => c.Id);

        // Alternate key (business key)
        builder.HasAlternateKey(c => c.Email);

        // Property configurations
        builder.Property(c => c.Name)
            .HasColumnName("FullName")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Email)
            .HasMaxLength(150)
            .IsRequired();

        // Generated properties
        builder.Property(c => c.Id)
            .AutoGenerated(true);  // Identity column

        builder.Property(c => c.CreatedAt)
            .IsReadOnly();  // Cannot be updated

        // Oracle sequence
        builder.Property(c => c.Id)
            .UseSequence("CUSTOMER_SEQ");

        // Read-only entity
        builder.IsReadOnly();
    });
}
```

#### Configuration Classes

```csharp
public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers", "dbo");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(150).IsRequired();
    }
}

// Apply configuration
protected override void OnModelCreating(DapperModelBuilder modelBuilder)
{
    // Option 1: Apply manually
    modelBuilder.ApplyConfiguration(new CustomerConfiguration());

    // Option 2: Auto-discover from assembly
    modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

    // Option 3: Auto-discover with filter
    modelBuilder.ApplyConfigurationsFromAssembly(
        Assembly.GetExecutingAssembly(),
        type => type.Namespace?.StartsWith("MyApp.Configurations") == true);
}
```

### Data Annotations

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nahmadov.DapperForge.Core.Attributes;

[Table("Customers", Schema = "dbo")]
public class Customer
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("FullName")]
    public string Name { get; set; }

    [Required]
    [MaxLength(150)]
    [EmailAddress]
    public string Email { get; set; }

    [StringLength(20, MinimumLength = 10)]
    public string PhoneNumber { get; set; }

    [ReadOnly]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey(nameof(Address))]
    public int? AddressId { get; set; }

    public Address Address { get; set; }
}

[ReadOnlyEntity]
public class AuditLog
{
    [Key]
    public int Id { get; set; }
    public string Action { get; set; }
    public DateTime Timestamp { get; set; }
}
```

### Convention-Based Mapping

**Default Conventions:**
1. **Table Name:** Entity type name (e.g., `Customer` → `Customers`)
2. **Primary Key:** Property named `Id` or `{TypeName}Id` (e.g., `CustomerId`)
3. **Column Name:** Property name
4. **Schema:** Dialect's default schema (SQL Server: `dbo`, Oracle: none)
5. **Generated Keys:** Auto-detected identity columns

**Key Discovery Order:**
1. Properties with `[Key]` attribute
2. Property named `Id`
3. Property named `{TypeName}Id`
4. Alternate keys with `HasAlternateKey()`

### Relationship Configuration

DapperForge supports configuring foreign key relationships using either fluent API or attributes.

#### Fluent API (Recommended)

```csharp
protected override void OnModelCreating(DapperModelBuilder modelBuilder)
{
    // One-to-Many: Order belongs to Customer
    modelBuilder.Entity<Order>(b =>
    {
        b.HasOne<Customer>(o => o.Customer)
         .WithMany(c => c.Orders)
         .HasForeignKey(o => o.CustomerId)
         .HasPrincipalKey(c => c.Id);  // Optional, defaults to PK
    });

    // One-to-One: UserProfile belongs to User
    modelBuilder.Entity<UserProfile>(b =>
    {
        b.HasOne<User>(p => p.User)
         .WithOne()
         .HasForeignKey(p => p.UserId);
    });

    // Configure from principal side (HasMany)
    modelBuilder.Entity<Customer>(b =>
    {
        b.HasMany<Order>(c => c.Orders)
         .WithOne(o => o.Customer)
         .HasForeignKey(o => o.CustomerId);
    });
}
```

**Available Methods:**

| Method | Description |
|--------|-------------|
| `HasOne<TRelated>(x => x.Nav)` | Configures a reference navigation (many-to-one or one-to-one) |
| `HasMany<TRelated>(x => x.Collection)` | Configures a collection navigation (one-to-many) |
| `WithMany()` | Specifies the inverse is a collection |
| `WithMany(x => x.Collection)` | Specifies the inverse collection navigation |
| `WithOne()` | Specifies the inverse is a reference |
| `WithOne(x => x.Nav)` | Specifies the inverse reference navigation |
| `HasForeignKey(x => x.FkProp)` | Specifies the foreign key property (required) |
| `HasPrincipalKey(x => x.PkProp)` | Specifies the principal key (defaults to "Id") |

#### ForeignKey Attribute

```csharp
public class Order
{
    public int Id { get; set; }

    [ForeignKey(nameof(Customer), typeof(Customer), nameof(Customer.Id))]
    public int CustomerId { get; set; }

    public Customer? Customer { get; set; }
}
```

**Attribute Parameters:**
- `navigationPropertyName` - Name of the navigation property (e.g., `"Customer"`)
- `principalEntityType` - The related entity type (e.g., `typeof(Customer)`)
- `principalKeyPropertyName` - Optional principal key property (defaults to `"Id"`)

#### Fluent vs Attribute Priority

When both fluent API and `[ForeignKey]` attribute are used for the same navigation property, **fluent configuration takes precedence**.

```csharp
// This entity has [ForeignKey] attribute on CustomerId
public class Order
{
    [ForeignKey(nameof(Customer), typeof(Customer))]
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
}

// Fluent config overrides the attribute
modelBuilder.Entity<Order>(b =>
{
    b.HasOne<Customer>(o => o.Customer)
     .WithMany()
     .HasForeignKey(o => o.CustomerId);
});
```

---

## Query Operations

### Simple Queries

```csharp
// Get all entities
IEnumerable<Customer> all = await db.Customers.GetAllAsync();

// Find by primary key
Customer? customer = await db.Customers.FindAsync(123);

// Check if any match
bool hasActive = await db.Customers.AnyAsync(c => c.IsActive);

// Check if all match
bool allActive = await db.Customers.AllAsync(c => c.IsActive);

// Count matching
long count = await db.Customers.CountAsync(c => c.IsActive);

// First matching (throws if not found)
Customer first = await db.Customers.FirstAsync(c => c.Email == email);

// First or default
Customer? firstOrDefault = await db.Customers.FirstOrDefaultAsync(c => c.Email == email);
```

### Filtered Queries

```csharp
// Simple filter
var active = await db.Customers.WhereAsync(c => c.IsActive);

// Complex filter
var results = await db.Customers.WhereAsync(c =>
    c.IsActive &&
    c.Name.StartsWith("John") &&
    c.CreatedAt > DateTime.Now.AddDays(-30));

// Case-insensitive filter
var customers = await db.Customers.WhereAsync(
    c => c.Name.Contains("john"),
    ignoreCase: true);

// IN clause
var ids = new[] { 1, 2, 3, 4, 5 };
var inList = await db.Customers.WhereAsync(c => ids.Contains(c.Id));
```

### Fluent Query API

```csharp
// Basic query
var customers = await db.Customers
    .Query()
    .Where(c => c.IsActive)
    .OrderBy(c => c.Name)
    .ToListAsync();

// Pagination
var page = await db.Customers
    .Query()
    .Where(c => c.IsActive)
    .OrderBy(c => c.Id)
    .Skip(20)
    .Take(10)
    .ToListAsync();

// Multiple ordering
var sorted = await db.Customers
    .Query()
    .OrderBy(c => c.City)
    .ThenByDescending(c => c.CreatedAt)
    .ToListAsync();

// Distinct
var unique = await db.Customers
    .Query()
    .Distinct()
    .ToListAsync();

// First/Last
var first = await db.Customers
    .Query()
    .Where(c => c.IsActive)
    .OrderBy(c => c.Name)
    .FirstAsync();

var last = await db.Customers
    .Query()
    .Where(c => c.IsActive)
    .OrderBy(c => c.Name)
    .LastAsync();

// Single (throws if 0 or >1 results)
var single = await db.Customers
    .Query()
    .Where(c => c.Id == 123)
    .SingleAsync();
```

### Raw SQL Queries

```csharp
// Query entities
var customers = await db.QueryAsync<Customer>(
    "SELECT * FROM Customers WHERE IsActive = @IsActive",
    new { IsActive = true });

// Query scalar value
var count = await db.QueryFirstOrDefaultAsync<int>(
    "SELECT COUNT(*) FROM Customers WHERE IsActive = @IsActive",
    new { IsActive = true });

// Query anonymous type
var results = await db.QueryAsync<dynamic>(
    @"SELECT c.Name, COUNT(o.Id) as OrderCount
      FROM Customers c
      LEFT JOIN Orders o ON c.Id = o.CustomerId
      GROUP BY c.Name");

// Query with transaction
var transaction = await db.BeginTransactionScopeAsync();
try
{
    var customers = await db.QueryAsync<Customer>(
        "SELECT * FROM Customers",
        transaction: transaction.Transaction);

    transaction.Complete();
}
finally
{
    transaction.Dispose();
}
```

---

## Mutation Operations

### Insert Operations

```csharp
// Simple insert
var customer = new Customer
{
    Name = "John Doe",
    Email = "john@example.com",
    IsActive = true,
    CreatedAt = DateTime.UtcNow
};

int rowsAffected = await db.Customers.InsertAsync(customer);

// Insert and get generated ID
int customerId = await db.Customers.InsertAndGetIdAsync<int>(customer);

// Insert with transaction
var transaction = await db.BeginTransactionScopeAsync();
try
{
    var id = await db.Customers.InsertAndGetIdAsync<int>(customer, transaction.Transaction);
    transaction.Complete();
}
finally
{
    transaction.Dispose();
}
```

### Update Operations

```csharp
// Update by primary key
var customer = await db.Customers.FindAsync(123);
customer.Name = "Jane Doe";
customer.Email = "jane@example.com";
int rowsAffected = await db.Customers.UpdateAsync(customer);

// Update with explicit WHERE
await db.Customers.UpdateAsync(
    new Customer { IsActive = false },
    where: new { CreatedAt = DateTime.UtcNow.AddYears(-5) },
    allowMultiple: true,
    expectedRows: null);

// Update with transaction
var transaction = await db.BeginTransactionScopeAsync();
try
{
    await db.Customers.UpdateAsync(customer, transaction.Transaction);
    transaction.Complete();
}
finally
{
    transaction.Dispose();
}
```

### Delete Operations

```csharp
// Delete by entity (uses primary key)
var customer = await db.Customers.FindAsync(123);
int rowsAffected = await db.Customers.DeleteAsync(customer);

// Delete by ID
rowsAffected = await db.Customers.DeleteByIdAsync(123);

// Mass delete with explicit WHERE
await db.Customers.DeleteAsync(
    where: new { IsActive = false, CreatedAt = DateTime.UtcNow.AddYears(-10) },
    allowMultiple: true,
    expectedRows: null);

// Delete with transaction
var transaction = await db.BeginTransactionScopeAsync();
try
{
    await db.Customers.DeleteByIdAsync(123, transaction.Transaction);
    transaction.Complete();
}
finally
{
    transaction.Dispose();
}
```

---

## Include & ThenInclude

### Single Navigation Property

```csharp
// Include single reference (many-to-one)
var orders = await db.Orders
    .Query()
    .Include(o => o.Customer)
    .ToListAsync();

// Include collection (one-to-many)
var customers = await db.Customers
    .Query()
    .Include(c => c.Orders)
    .ToListAsync();
```

### Nested Navigation (ThenInclude)

```csharp
// Include nested reference
var orders = await db.Orders
    .Query()
    .Include(o => o.Customer)
        .ThenInclude(c => c.Address)
    .ToListAsync();

// Include nested collection
var customers = await db.Customers
    .Query()
    .Include(c => c.Orders)
        .ThenInclude(o => o.OrderItems)
    .ToListAsync();
```

### Multiple Include Paths

```csharp
var orders = await db.Orders
    .Query()
    .Include(o => o.Customer)
        .ThenInclude(c => c.Address)
    .Include(o => o.OrderItems)
        .ThenInclude(i => i.Product)
    .ToListAsync();
```

### Query Splitting Strategies

#### AsSingleQuery (Default)

**Best for:**
- Single reference navigation
- Small result sets (<100 rows)
- Need atomic consistency

```csharp
var orders = await db.Orders
    .Query()
    .Include(o => o.Customer)
    .AsSingleQuery()
    .ToListAsync();

// Generated SQL:
// SELECT o.*, c.*
// FROM Orders o
// LEFT JOIN Customers c ON o.CustomerId = c.Id
```

**Advantages:**
- Single database round-trip
- Atomic consistency (single snapshot)

**Disadvantages:**
- Cartesian explosion with collections
- Duplicate data transfer

#### AsSplitQuery

**Best for:**
- Collection navigation
- Large result sets (>100 rows)
- Multiple includes

```csharp
var orders = await db.Orders
    .Query()
    .Include(o => o.Customer)
        .ThenInclude(c => c.Address)
    .Include(o => o.OrderItems)
        .ThenInclude(i => i.Product)
    .AsSplitQuery()
    .ToListAsync();

// Generated SQL (multiple queries):
// Query 1: SELECT * FROM Orders
// Query 2: SELECT * FROM Customers WHERE Id IN (@id1, @id2, ...)
// Query 3: SELECT * FROM Addresses WHERE CustomerId IN (@cid1, @cid2, ...)
// Query 4: SELECT * FROM OrderItems WHERE OrderId IN (@oid1, @oid2, ...)
// Query 5: SELECT * FROM Products WHERE Id IN (@pid1, @pid2, ...)
```

**Advantages:**
- No cartesian explosion
- Less network transfer

**Disadvantages:**
- Multiple round-trips
- Potential consistency issues

### Identity Resolution

```csharp
// Default: Prevents duplicate entity instances
var customers = await db.Customers
    .Query()
    .Include(c => c.Orders)
    .ToListAsync();
// Each Customer instance appears only once, shared across orders

// Disable identity resolution
var customers = await db.Customers
    .Query()
    .Include(c => c.Orders)
    .AsNoIdentityResolution()
    .ToListAsync();
// New Customer instance created per order
```

---

## Transaction Management

### Basic Transaction Scope

```csharp
using var scope = await db.BeginTransactionScopeAsync();
try
{
    await db.Customers.InsertAsync(customer, scope.Transaction);
    await db.Orders.InsertAsync(order, scope.Transaction);

    scope.Complete();  // Mark for commit
}
finally
{
    scope.Dispose();  // Auto-commits if Complete() called, else rolls back
}
```

### Isolation Levels

```csharp
// Read Committed (default)
var scope = await db.BeginTransactionScopeAsync(IsolationLevel.ReadCommitted);

// Read Uncommitted
var scope = await db.BeginTransactionScopeAsync(IsolationLevel.ReadUncommitted);

// Repeatable Read
var scope = await db.BeginTransactionScopeAsync(IsolationLevel.RepeatableRead);

// Serializable
var scope = await db.BeginTransactionScopeAsync(IsolationLevel.Serializable);

// Snapshot (SQL Server)
var scope = await db.BeginTransactionScopeAsync(IsolationLevel.Snapshot);
```

### Manual Commit/Rollback

```csharp
using var scope = await db.BeginTransactionScopeAsync();
try
{
    await db.Customers.InsertAsync(customer, scope.Transaction);

    if (someCondition)
    {
        scope.Commit();  // Manual commit
    }
    else
    {
        scope.Rollback();  // Manual rollback
    }
}
finally
{
    scope.Dispose();
}
```

### Nested Operations

```csharp
using var scope = await db.BeginTransactionScopeAsync();
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

### Transaction with Validation

```csharp
using var scope = await db.BeginTransactionScopeAsync();
try
{
    var customer = new Customer
    {
        Name = "",  // Invalid - will throw DapperValidationException
        Email = "test@example.com"
    };

    await db.Customers.InsertAsync(customer, scope.Transaction);
    scope.Complete();
}
catch (DapperValidationException ex)
{
    // Transaction automatically rolled back (Complete() not called)
    Console.WriteLine($"Validation errors: {string.Join(", ", ex.Errors)}");
}
finally
{
    scope.Dispose();
}
```

---

## Validation System

### Fluent Validation Rules

```csharp
modelBuilder.Entity<Customer>(builder =>
{
    builder.Property(c => c.Name)
        .IsRequired()
        .HasMaxLength(100);

    builder.Property(c => c.Email)
        .IsRequired()
        .HasMaxLength(150);

    builder.Property(c => c.PhoneNumber)
        .HasMaxLength(20);
});

// Throws DapperValidationException if validation fails
await db.Customers.InsertAsync(customer);
```

### Data Annotation Validation

```csharp
public class Customer
{
    [Required(ErrorMessage = "Name is required")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    public string Name { get; set; }

    [Required]
    [MaxLength(150)]
    [EmailAddress]
    public string Email { get; set; }

    [StringLength(20, MinimumLength = 10,
        ErrorMessage = "Phone number must be between 10 and 20 characters")]
    public string PhoneNumber { get; set; }

    [Range(18, 120, ErrorMessage = "Age must be between 18 and 120")]
    public int Age { get; set; }
}
```

### Validation Exceptions

```csharp
try
{
    var customer = new Customer
    {
        Name = null,  // Required field
        Email = "a".PadRight(200, 'a')  // Exceeds max length
    };

    await db.Customers.InsertAsync(customer);
}
catch (DapperValidationException ex)
{
    Console.WriteLine($"Validation failed: {ex.Message}");

    foreach (var error in ex.Errors)
    {
        Console.WriteLine($"{error.PropertyName}: {error.ErrorMessage}");
    }
}
```

### Read-Only Entities

```csharp
// Using attribute
[ReadOnlyEntity]
public class AuditLog
{
    public int Id { get; set; }
    public string Action { get; set; }
    public DateTime Timestamp { get; set; }
}

// Using fluent API
modelBuilder.Entity<AuditLog>(builder =>
{
    builder.IsReadOnly();
});

// Throws DapperReadOnlyException
await db.AuditLogs.InsertAsync(log);
await db.AuditLogs.UpdateAsync(log);
await db.AuditLogs.DeleteAsync(log);

// Queries work fine
var logs = await db.AuditLogs.GetAllAsync();
```

### Read-Only Properties

```csharp
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }

    [ReadOnly]  // Excluded from INSERT/UPDATE
    public DateTime CreatedAt { get; set; }

    [ReadOnly]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime? LastModified { get; set; }
}

// Fluent API
modelBuilder.Entity<Customer>(builder =>
{
    builder.Property(c => c.CreatedAt).IsReadOnly();
    builder.Property(c => c.LastModified).IsReadOnly();
});
```

---

## SQL Generation

### Generated SQL Statements

```csharp
// For each entity type, SqlGenerator pre-generates these SQL statements:
public class SqlGenerator<TEntity>
{
    public string SelectAllSql { get; }
    public string SelectByIdSql { get; }
    public string InsertSql { get; }
    public string? InsertReturningIdSql { get; }
    public string UpdateSql { get; }
    public string DeleteByIdSql { get; }
}
```

### SQL Server Example

```sql
-- SelectAllSql
SELECT a.[Id], a.[Name], a.[Email], a.[City], a.[IsActive], a.[CreatedAt]
FROM [dbo].[Customers] AS a

-- SelectByIdSql
SELECT a.[Id], a.[Name], a.[Email], a.[City], a.[IsActive], a.[CreatedAt]
FROM [dbo].[Customers] AS a
WHERE a.[Id] = @Id

-- InsertSql
INSERT INTO [dbo].[Customers] ([Name], [Email], [City], [IsActive])
VALUES (@Name, @Email, @City, @IsActive)

-- InsertReturningIdSql
INSERT INTO [dbo].[Customers] ([Name], [Email], [City], [IsActive])
VALUES (@Name, @Email, @City, @IsActive);
SELECT CAST(SCOPE_IDENTITY() AS int)

-- UpdateSql
UPDATE [dbo].[Customers]
SET [Name] = @Name, [Email] = @Email, [City] = @City, [IsActive] = @IsActive
WHERE [Id] = @Id

-- DeleteByIdSql
DELETE FROM [dbo].[Customers] WHERE [Id] = @Id
```

### Oracle Example

```sql
-- SelectAllSql
SELECT a."Id", a."Name", a."Email", a."City", a."IsActive", a."CreatedAt"
FROM "Customers" a

-- SelectByIdSql
SELECT a."Id", a."Name", a."Email", a."City", a."IsActive", a."CreatedAt"
FROM "Customers" a
WHERE a."Id" = :Id

-- InsertSql
INSERT INTO "Customers" ("Name", "Email", "City", "IsActive")
VALUES (:Name, :Email, :City, :IsActive)

-- InsertReturningIdSql (using sequence)
INSERT INTO "Customers" ("Id", "Name", "Email", "City", "IsActive")
VALUES (CUSTOMER_SEQ.NEXTVAL, :Name, :Email, :City, :IsActive)
RETURNING "Id" INTO :Id

-- UpdateSql
UPDATE "Customers"
SET "Name" = :Name, "Email" = :Email, "City" = :City, "IsActive" = :IsActive
WHERE "Id" = :Id

-- DeleteByIdSql
DELETE FROM "Customers" WHERE "Id" = :Id
```

---

## Expression Translation

### Complete Translation Reference

| C# Expression | SQL Output | Notes |
|---------------|------------|-------|
| `c.Id == 5` | `a.[Id] = @p0` | Equality |
| `c.Status != "Closed"` | `a.[Status] != @p0` | Inequality |
| `c.Age > 18` | `a.[Age] > @p0` | Greater than |
| `c.Price >= 100` | `a.[Price] >= @p0` | Greater or equal |
| `c.Count < 10` | `a.[Count] < @p0` | Less than |
| `c.Score <= 50` | `a.[Score] <= @p0` | Less or equal |
| `c.Email == null` | `a.[Email] IS NULL` | Null check |
| `c.Email != null` | `a.[Email] IS NOT NULL` | Not null |
| `c.IsActive` | `a.[IsActive] = 1` | Boolean true |
| `!c.IsActive` | `a.[IsActive] = 0` | Boolean false |
| `c.IsActive == true` | `a.[IsActive] = 1` | Explicit true |
| `c.IsActive == false` | `a.[IsActive] = 0` | Explicit false |
| `c.Name.StartsWith("A")` | `a.[Name] LIKE @p0` | Parameter: "A%" |
| `c.Email.EndsWith(".com")` | `a.[Email] LIKE @p0` | Parameter: "%.com" |
| `c.City.Contains("New")` | `a.[City] LIKE @p0` | Parameter: "%New%" |
| `ids.Contains(c.Id)` | `a.[Id] IN (1,2,3)` | IN clause |
| `[].Contains(c.Id)` | `1=0` | Empty IN |
| `c.IsActive && c.Age > 18` | `... AND ...` | Logical AND |
| `c.City == "NY" \|\| c.City == "LA"` | `... OR ...` | Logical OR |
| `!(c.IsActive && c.Age > 18)` | `NOT (...)` | Logical NOT |

### Case-Insensitive Comparisons

```csharp
// Regular comparison (case-sensitive on Oracle, depends on collation on SQL Server)
var customers = await db.Customers.WhereAsync(c => c.Name == "alice");
// SQL Server: WHERE a.[Name] = @p0
// Oracle: WHERE a."Name" = :p0

// Case-insensitive comparison (cross-database compatible)
var customers = await db.Customers.WhereAsync(
    c => c.Name == "alice",
    ignoreCase: true);
// SQL Server: WHERE LOWER(a.[Name]) = LOWER(@p0)
// Oracle: WHERE LOWER(a."Name") = LOWER(:p0)
```

### Complex Expressions

```csharp
// Complex logical expression
var results = await db.Customers.WhereAsync(c =>
    (c.IsActive || c.Id > 100) &&
    (c.Name.StartsWith("A") || c.Name.StartsWith("B")) &&
    c.Email != null &&
    c.City.Contains("New"));

// Generated SQL:
// WHERE ((a.[IsActive] = 1 OR a.[Id] > @p0) AND
//        (a.[Name] LIKE @p1 OR a.[Name] LIKE @p2) AND
//        a.[Email] IS NOT NULL AND
//        a.[City] LIKE @p3)
// Parameters: { p0: 100, p1: "A%", p2: "B%", p3: "%New%" }
```

---

## Connection Management

### Connection Scopes

```csharp
// Automatic connection management (recommended)
var customers = await db.Customers.GetAllAsync();
// Connection opened, used, and closed automatically

// Manual connection scope
using (var scope = db.CreateConnectionScope())
{
    var connection = scope.Connection;

    // Use connection for multiple operations
    var customers = await connection.QueryAsync<Customer>("SELECT * FROM Customers");
    await connection.ExecuteAsync("UPDATE Customers SET ...");

    // Connection automatically closed on dispose
}
```

### Connection Pooling

DapperForge leverages ADO.NET connection pooling:

```csharp
// Connection string with pooling settings (SQL Server)
var connectionString = "Server=.;Database=MyDb;Integrated Security=true;" +
                      "Min Pool Size=5;" +
                      "Max Pool Size=100;" +
                      "Connection Timeout=30;";

// Oracle connection string
var connectionString = "Data Source=MyOracle;User Id=myuser;Password=mypass;" +
                      "Min Pool Size=5;" +
                      "Max Pool Size=100;" +
                      "Connection Timeout=30;";
```

### Retry Logic

**Automatic Retry for Queries:**
```csharp
// Query operations automatically retry on transient failures
var customers = await db.Customers.GetAllAsync();
// Retry sequence on transient error:
// - Attempt 1: Immediate
// - Attempt 2: 100ms delay
// - Attempt 3: 200ms delay
// - Attempt 4: 400ms delay (exponential backoff)
```

**Transient Errors Detected:**
- SQL Server: Timeout (-2), Deadlock (1205), Azure transient codes
- Oracle: Deadlock (ORA-00060), Cancel (ORA-01013)
- Generic: Timeout exceptions, connection failures

**Configuration:**
```csharp
services.AddDapperDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    options.MaxRetryCount = 3;                  // Max retry attempts
    options.RetryDelayMilliseconds = 100;       // Base delay
    options.CommandTimeoutSeconds = 30;         // Command timeout
});
```

**Note:** Mutation operations (INSERT/UPDATE/DELETE) are NOT automatically retried to prevent duplicate operations.

### Health Checks

```csharp
// Check database connectivity
bool isHealthy = await db.HealthCheckAsync();

if (!isHealthy)
{
    // Log alert, retry, or fail gracefully
    logger.LogError("Database health check failed");
}
```

**Health Check Query:**
- SQL Server: `SELECT 1`
- Oracle: `SELECT 1 FROM DUAL`

---

## Performance & Caching

### Expression Compilation Cache

**Purpose:** Avoid repeated compilation of LINQ expression trees.

**Configuration:**
- Cache size: 1000 expressions (LRU eviction)
- Thread-safe: `ConcurrentDictionary`
- Automatic: No configuration needed

**How it works:**
```csharp
// First call: Compiles expression and caches it
var users1 = await db.Users.WhereAsync(u => u.IsActive);
// Compilation time: ~5ms

// Second call: Reuses cached compiled expression
var users2 = await db.Users.WhereAsync(u => u.IsActive);
// Compilation time: ~0ms (cache hit)
```

### SQL Statement Cache

**Purpose:** Avoid runtime SQL generation.

**Configuration:**
- One `SqlGenerator<TEntity>` per entity type
- All SQL statements pre-generated at initialization
- Cached for context lifetime

**Generated Statements:**
- `SelectAllSql`
- `SelectByIdSql`
- `InsertSql`
- `InsertReturningIdSql`
- `UpdateSql`
- `DeleteByIdSql`

### Identity Cache

**Purpose:** Prevent duplicate entity instances during Include operations.

**Configuration:**
- Max size: 10,000 entities per query (LRU eviction)
- Per-query scope (not shared across queries)
- Automatically disposed after query completion

**Behavior:**
```csharp
// With identity resolution (default)
var orders = await db.Orders
    .Include(o => o.Customer)
    .ToListAsync();
// Each Customer instance appears only once in memory

// Without identity resolution
var orders = await db.Orders
    .Include(o => o.Customer)
    .AsNoIdentityResolution()
    .ToListAsync();
// New Customer instance per order (more memory)
```

### Performance Tips

**1. Use Predicates for Filtering:**
```csharp
// GOOD: Predicate translates to WHERE clause
var active = await db.Users.WhereAsync(u => u.IsActive);

// AVOID: Fetches all rows, filters in memory
var all = await db.Users.GetAllAsync();
var active = all.Where(u => u.IsActive).ToList();
```

**2. Prefer FirstOrDefaultAsync:**
```csharp
// GOOD: Returns after first match
var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

// LESS EFFICIENT: Fetches all matches
var users = await db.Users.WhereAsync(u => u.Email == email);
var user = users.FirstOrDefault();
```

**3. Use Split Query for Collections:**
```csharp
// GOOD: No cartesian product
var customers = await db.Customers
    .Include(c => c.Orders)
    .AsSplitQuery()
    .ToListAsync();

// AVOID: Cartesian explosion
var customers = await db.Customers
    .Include(c => c.Orders)
    .AsSingleQuery()
    .ToListAsync();
```

**4. Leverage Expression Caching:**
```csharp
// GOOD: Cache common filters
private static readonly Expression<Func<Customer, bool>> ActiveFilter = c => c.IsActive;

var active1 = await db.Customers.WhereAsync(ActiveFilter);  // Compile and cache
var active2 = await db.Customers.WhereAsync(ActiveFilter);  // Cache hit
```

**5. Use Transactions for Bulk Operations:**
```csharp
// GOOD: Single transaction
using var scope = await db.BeginTransactionScopeAsync();
foreach (var customer in customers)
{
    await db.Customers.InsertAsync(customer, scope.Transaction);
}
scope.Complete();

// AVOID: Individual transactions (slower)
foreach (var customer in customers)
{
    await db.Customers.InsertAsync(customer);
}
```

---

## Database Dialects

### SQL Server Dialect

**Identifier Quoting:**
- Table: `[dbo].[Customers]`
- Column: `[Id]`, `[Name]`

**Parameter Prefix:** `@`
- Example: `@p0`, `@Id`, `@Name`

**Boolean Literals:**
- True: `1`
- False: `0`

**Default Schema:** `dbo`

**Identity Key Retrieval:**
```sql
INSERT INTO [dbo].[Customers] ([Name])
VALUES (@Name);
SELECT CAST(SCOPE_IDENTITY() AS int)
```

**Health Check Query:**
```sql
SELECT 1
```

### Oracle Dialect

**Identifier Quoting:**
- Table: `"Customers"` or `"SCHEMA"."Customers"`
- Column: `"Id"`, `"Name"`

**Parameter Prefix:** `:`
- Example: `:p0`, `:Id`, `:Name`

**Boolean Literals:**
- True: `1`
- False: `0`

**Default Schema:** None (uses current schema)

**Sequence Support:**
```sql
-- Using sequence
INSERT INTO "Customers" ("Id", "Name")
VALUES (CUSTOMER_SEQ.NEXTVAL, :Name)
RETURNING "Id" INTO :Id

-- Configure in fluent API
modelBuilder.Entity<Customer>(b =>
{
    b.Property(c => c.Id).UseSequence("CUSTOMER_SEQ");
});
```

**Health Check Query:**
```sql
SELECT 1 FROM DUAL
```

### Dialect-Specific Differences

| Feature | SQL Server | Oracle |
|---------|-----------|--------|
| Identifier Quoting | `[Table]`, `[Column]` | `"Table"`, `"Column"` |
| Parameter Prefix | `@` | `:` |
| Boolean Storage | `bit` (1/0) | `number(1)` (1/0) |
| Auto Keys | `IDENTITY`, `SCOPE_IDENTITY()` | Sequences, `NEXTVAL` |
| String Concat | `+` operator | `\|\|` operator |
| Top N | `TOP N` | `ROWNUM` or `FETCH FIRST` |
| Case Sensitivity | Collation-dependent | Case-sensitive by default |
| Date Function | `GETDATE()` | `SYSDATE` |
| Default Schema | `dbo` | Current user schema |

### Cross-Database Compatibility

```csharp
// Use ignoreCase for cross-database string comparisons
var customers = await db.Customers.WhereAsync(
    c => c.Name.Contains("john"),
    ignoreCase: true);
// SQL Server: WHERE LOWER(a.[Name]) LIKE LOWER(@p0)
// Oracle: WHERE LOWER(a."Name") LIKE LOWER(:p0)

// Let dialect handle schema defaults
modelBuilder.Entity<Customer>(b =>
{
    b.ToTable("Customers");  // Uses dialect's default schema
});
// SQL Server: [dbo].[Customers]
// Oracle: "Customers" (current schema)

// Use raw SQL for dialect-specific features
if (db.Dialect.Name == "SqlServer")
{
    var result = await db.QueryAsync<MyType>("SELECT TOP 10 ...");
}
else if (db.Dialect.Name == "Oracle")
{
    var result = await db.QueryAsync<MyType>("SELECT * FROM ... WHERE ROWNUM <= 10");
}
```

---

## Complete API Reference

### DapperDbContext

```csharp
public abstract class DapperDbContext : IDisposable
{
    // Constructor
    protected DapperDbContext(DapperDbContextOptions options);

    // Entity Sets
    protected DapperSet<TEntity> Set<TEntity>() where TEntity : class;

    // Low-level Dapper wrappers
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null);
    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, IDbTransaction? transaction = null);
    Task<int> ExecuteAsync(string sql, object? param = null, IDbTransaction? transaction = null);
    Task<List<TEntity?>> QueryWithTypesAsync<TEntity>(string sql, Type[] types, object parameters, string splitOn, Func<object?[], TEntity?> map, IDbTransaction? transaction = null);
    Task<IEnumerable<object>> QueryDynamicAsync(Type entityType, string sql, object? param = null, IDbTransaction? transaction = null);

    // Connection & Transaction Management
    IConnectionScope CreateConnectionScope();
    Task<ITransactionScope> BeginTransactionScopeAsync();
    Task<ITransactionScope> BeginTransactionScopeAsync(IsolationLevel isolationLevel);
    bool HasActiveTransaction { get; }

    // Health Check
    Task<bool> HealthCheckAsync();

    // Configuration Hook
    protected virtual void OnModelCreating(DapperModelBuilder modelBuilder);

    // Disposal
    void Dispose();
    protected virtual void Dispose(bool disposing);
}
```

### DapperSet<TEntity>

```csharp
public sealed class DapperSet<TEntity> where TEntity : class
{
    // Query Operations
    IDapperQueryable<TEntity> Query();
    Task<IEnumerable<TEntity>> GetAllAsync();
    Task<TEntity?> FindAsync(object key);
    Task<IEnumerable<TEntity>> WhereAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false);
    Task<TEntity> FirstAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false);
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false);
    Task<bool> AllAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false);
    Task<long> CountAsync(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false);

    // Mutation Operations
    Task<int> InsertAsync(TEntity entity, IDbTransaction? transaction = null);
    Task<TKey> InsertAndGetIdAsync<TKey>(TEntity entity, IDbTransaction? transaction = null);
    Task<int> UpdateAsync(TEntity entity, IDbTransaction? transaction = null);
    Task<int> UpdateAsync(TEntity entity, object where, bool allowMultiple = false, int? expectedRows = null, IDbTransaction? transaction = null);
    Task<int> DeleteAsync(TEntity entity, IDbTransaction? transaction = null);
    Task<int> DeleteByIdAsync(object key, IDbTransaction? transaction = null);
    Task<int> DeleteAsync(object where, bool allowMultiple = false, int? expectedRows = null, IDbTransaction? transaction = null);
}
```

### IDapperQueryable<TEntity>

```csharp
public interface IDapperQueryable<TEntity> where TEntity : class
{
    // Filtering
    IDapperQueryable<TEntity> Where(Expression<Func<TEntity, bool>> predicate, bool ignoreCase = false);

    // Ordering
    IDapperQueryable<TEntity> OrderBy(Expression<Func<TEntity, object?>> keySelector);
    IDapperQueryable<TEntity> OrderByDescending(Expression<Func<TEntity, object?>> keySelector);
    IDapperQueryable<TEntity> ThenBy(Expression<Func<TEntity, object?>> keySelector);
    IDapperQueryable<TEntity> ThenByDescending(Expression<Func<TEntity, object?>> keySelector);

    // Pagination
    IDapperQueryable<TEntity> Skip(int count);
    IDapperQueryable<TEntity> Take(int count);

    // Distinct
    IDapperQueryable<TEntity> Distinct();

    // Include
    IIncludableQueryable<TEntity, TProperty> Include<TProperty>(Expression<Func<TEntity, TProperty>> navigationSelector);
    IIncludableQueryable<TEntity, TNextProperty> ThenInclude<TPrevious, TNextProperty>(Expression<Func<TPrevious, TNextProperty>> navigationSelector) where TPrevious : class;

    // Query Splitting
    IDapperQueryable<TEntity> AsSplitQuery();
    IDapperQueryable<TEntity> AsSingleQuery();
    IDapperQueryable<TEntity> AsNoIdentityResolution();

    // Execution
    Task<IEnumerable<TEntity>> ToListAsync();
    Task<TEntity> FirstAsync();
    Task<TEntity?> FirstOrDefaultAsync();
    Task<TEntity> SingleAsync();
    Task<TEntity?> SingleOrDefaultAsync();
    Task<TEntity> LastAsync();
    Task<TEntity?> LastOrDefaultAsync();
    Task<bool> AnyAsync();
    Task<long> CountAsync();
}
```

### DapperModelBuilder

```csharp
public class DapperModelBuilder
{
    // Constructor
    public DapperModelBuilder(ISqlDialect dialect, string? defaultSchema = null);

    // Properties
    ISqlDialect Dialect { get; }
    string? DefaultSchema { get; }

    // Entity Configuration
    EntityTypeBuilder<TEntity> Entity<TEntity>();
    EntityTypeBuilder<TEntity> Entity<TEntity>(Action<EntityTypeBuilder<TEntity>> configure);
    IEntityTypeBuilder Entity(Type clrType);
    IEntityTypeBuilder Entity(Type clrType, Action<IEntityTypeBuilder> configure);

    // Apply Configurations
    void ApplyConfiguration<TEntity>(IEntityTypeConfiguration<TEntity> configuration);
    void ApplyConfigurationsFromAssembly(Assembly assembly, Func<Type, bool>? predicate = null);

    // Build
    IReadOnlyDictionary<Type, EntityMapping> Build();
}
```

### EntityTypeBuilder<TEntity>

```csharp
public class EntityTypeBuilder<TEntity>
{
    // Table Mapping
    EntityTypeBuilder<TEntity> ToTable(string tableName);
    EntityTypeBuilder<TEntity> ToTable(string tableName, string schema);

    // Keys
    EntityTypeBuilder<TEntity> HasKey(Expression<Func<TEntity, object?>> keyExpression);
    EntityTypeBuilder<TEntity> HasAlternateKey(Expression<Func<TEntity, object?>> keyExpression);

    // Properties
    PropertyBuilder<TProperty> Property<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression);

    // Read-Only
    EntityTypeBuilder<TEntity> IsReadOnly();
}
```

### PropertyBuilder<TProperty>

```csharp
public class PropertyBuilder<TProperty>
{
    // Column Mapping
    PropertyBuilder<TProperty> HasColumnName(string columnName);

    // Validation
    PropertyBuilder<TProperty> IsRequired();
    PropertyBuilder<TProperty> HasMaxLength(int maxLength);

    // Generation
    PropertyBuilder<TProperty> AutoGenerated(bool isGenerated);
    PropertyBuilder<TProperty> UseSequence(string sequenceName);

    // Read-Only
    PropertyBuilder<TProperty> IsReadOnly();
}
```

### ITransactionScope

```csharp
public interface ITransactionScope : IDisposable
{
    IDbTransaction Transaction { get; }
    void Complete();
    void Commit();
    void Rollback();
}
```

### IConnectionScope

```csharp
public interface IConnectionScope : IDisposable
{
    IDbConnection Connection { get; }
}
```

### Exceptions

```csharp
// Base exception
public class DapperForgeException : Exception

// Configuration error
public class DapperConfigurationException : DapperForgeException

// Validation error
public class DapperValidationException : DapperForgeException
{
    IReadOnlyList<ValidationError> Errors { get; }
}

// Read-only violation
public class DapperReadOnlyException : DapperForgeException
```

---

## Best Practices

### 1. Use Scoped Lifetime

```csharp
// GOOD: Scoped lifetime (default)
services.AddDapperDbContext<AppDbContext>(
    options => options.UseSqlServer(connectionString),
    ServiceLifetime.Scoped);

// AVOID: Singleton lifetime
services.AddDapperDbContext<AppDbContext>(
    options => options.UseSqlServer(connectionString),
    ServiceLifetime.Singleton);
// Causes: Connection pool exhaustion, memory leaks, warnings
```

### 2. Dispose Contexts Properly

```csharp
// GOOD: Using statement
await using var db = serviceProvider.GetRequiredService<AppDbContext>();
var users = await db.Users.GetAllAsync();

// GOOD: ASP.NET Core DI auto-disposes scoped services
public class UserService
{
    private readonly AppDbContext _db;

    public UserService(AppDbContext db) => _db = db;

    public Task<List<User>> GetActiveUsers()
        => _db.Users.WhereAsync(u => u.IsActive);
}
```

### 3. Prefer Split Query for Collections

```csharp
// GOOD: Split query avoids cartesian product
var customers = await db.Customers
    .Include(c => c.Orders)
    .AsSplitQuery()
    .ToListAsync();

// AVOID: Single query with collections
var customers = await db.Customers
    .Include(c => c.Orders)
    .AsSingleQuery()
    .ToListAsync();
// Problem: If 1 customer has 10 orders, customer data duplicated 10 times
```

### 4. Use Transactions for Related Operations

```csharp
// GOOD: Atomic operation
using var scope = await db.BeginTransactionScopeAsync();
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

// AVOID: Separate operations (not atomic)
await db.Customers.InsertAsync(customer);
await db.Orders.InsertAsync(order);  // Could fail, leaving orphaned customer
```

### 5. Leverage Expression Caching

```csharp
// GOOD: Cache common filters as static fields
public class CustomerRepository
{
    private static readonly Expression<Func<Customer, bool>> ActiveFilter = c => c.IsActive;
    private static readonly Expression<Func<Customer, bool>> InactiveFilter = c => !c.IsActive;

    public Task<List<Customer>> GetActiveAsync()
        => _db.Customers.WhereAsync(ActiveFilter);  // Cache hit after first call

    public Task<List<Customer>> GetInactiveAsync()
        => _db.Customers.WhereAsync(InactiveFilter);  // Cache hit after first call
}
```

### 6. Use Predicates for Filtering

```csharp
// GOOD: Predicate translates to WHERE clause
var active = await db.Users.WhereAsync(u => u.IsActive);

// AVOID: In-memory filtering
var all = await db.Users.GetAllAsync();
var active = all.Where(u => u.IsActive).ToList();
```

### 7. Handle Validation Errors Gracefully

```csharp
try
{
    await db.Customers.InsertAsync(customer);
}
catch (DapperValidationException ex)
{
    // Return friendly error messages to user
    return BadRequest(new
    {
        Message = "Validation failed",
        Errors = ex.Errors.Select(e => new
        {
            Field = e.PropertyName,
            Error = e.ErrorMessage
        })
    });
}
```

### 8. Use Configuration Classes for Complex Mappings

```csharp
// GOOD: Separate configuration class
public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers", "dbo");
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        // ... more configuration
    }
}

// Apply in OnModelCreating
protected override void OnModelCreating(DapperModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
}
```

### 9. Use Raw SQL for Complex Queries

```csharp
// GOOD: Use raw SQL for aggregations, grouping, complex joins
var report = await db.QueryAsync<ReportDto>(@"
    SELECT
        c.Name as CustomerName,
        COUNT(o.Id) as OrderCount,
        SUM(o.Total) as TotalRevenue
    FROM Customers c
    LEFT JOIN Orders o ON c.Id = o.CustomerId
    GROUP BY c.Name
    HAVING COUNT(o.Id) > 10
    ORDER BY TotalRevenue DESC");

// AVOID: Fetching all data and aggregating in memory
var customers = await db.Customers.GetAllAsync();
var orders = await db.Orders.GetAllAsync();
// ... complex LINQ operations in memory
```

### 10. Log SQL in Development

```csharp
services.AddDapperDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString);

    if (env.IsDevelopment())
    {
        options.EnableSqlLogging = true;
        options.Logger = loggerFactory.CreateLogger<AppDbContext>();
    }
});
```

---

## Migration from EF Core

### API Mapping Table

| EF Core | DapperForge | Notes |
|---------|-------------|-------|
| `DbContext` | `DapperDbContext` | Base class |
| `DbSet<T>` | `DapperSet<T>` | Entity collection |
| `ToListAsync()` | `GetAllAsync()` or `.Query().ToListAsync()` | Fetch all |
| `FirstOrDefaultAsync()` | `FirstOrDefaultAsync()` | Same API |
| `SingleOrDefaultAsync()` | `SingleOrDefaultAsync()` | Same API |
| `AnyAsync()` | `AnyAsync()` | Same API |
| `CountAsync()` | `CountAsync()` | Same API |
| `FindAsync(id)` | `FindAsync(id)` | By primary key |
| `Where(...).ToListAsync()` | `WhereAsync(...)` | Expression filter |
| `Add(entity)` + `SaveChanges()` | `InsertAsync(entity)` | Immediate |
| `Update(entity)` + `SaveChanges()` | `UpdateAsync(entity)` | Immediate |
| `Remove(entity)` + `SaveChanges()` | `DeleteAsync(entity)` | Immediate |
| `Include(x => x.Nav)` | `Include(x => x.Nav)` | Same API |
| `ThenInclude(x => x.Nav)` | `ThenInclude(x => x.Nav)` | Same API |
| `AsNoTracking()` | N/A | Never tracks |
| `SaveChangesAsync()` | N/A | No SaveChanges |

### Step-by-Step Migration Guide

#### 1. Update Package References

```xml
<!-- Remove EF Core -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" Remove="true" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.0" Remove="true" />

<!-- Add DapperForge -->
<PackageReference Include="Nahmadov.DapperForge.Core" Version="2.0.0" />
<PackageReference Include="Nahmadov.DapperForge.SqlServer" Version="2.0.0" />
```

#### 2. Update Context Registration

```csharp
// EF Core
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// DapperForge
services.AddDapperDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));
```

#### 3. Update Context Class

```csharp
// EF Core
public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("Users");
            b.HasKey(u => u.Id);
            b.Property(u => u.Name).IsRequired().HasMaxLength(100);
        });
    }
}

// DapperForge
public class AppDbContext : DapperDbContext
{
    public DapperSet<User> Users => Set<User>();

    public AppDbContext(DapperDbContextOptions<AppDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(DapperModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("Users");
            // HasKey not needed - inferred from [Key] or Id
            b.Property(u => u.Name).IsRequired().HasMaxLength(100);
        });
    }
}
```

#### 4. Update Service Code

```csharp
// EF Core
public class UserService
{
    private readonly AppDbContext _context;

    public async Task<List<User>> GetActiveUsers()
    {
        return await _context.Users
            .Where(u => u.IsActive)
            .ToListAsync();
    }

    public async Task CreateUser(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
    }
}

// DapperForge
public class UserService
{
    private readonly AppDbContext _context;

    public async Task<List<User>> GetActiveUsers()
    {
        // Change Where().ToListAsync() to WhereAsync()
        return await _context.Users.WhereAsync(u => u.IsActive);
    }

    public async Task CreateUser(User user)
    {
        // Remove Add() + SaveChangesAsync(), use InsertAsync()
        await _context.Users.InsertAsync(user);
    }
}
```

#### 5. Handle Change Tracking

```csharp
// EF Core - Implicit update via change tracking
var user = await _context.Users.FindAsync(id);
user.Name = "Updated";
await _context.SaveChangesAsync();  // Detects changes

// DapperForge - Explicit update
var user = await _context.Users.FindAsync(id);
user.Name = "Updated";
await _context.Users.UpdateAsync(user);  // Explicit update call
```

---

## Limitations & Known Issues

### Current Limitations (v2.0.0)

1. **Single-column keys only** - No composite key support
   - Workaround: Use alternate keys or raw SQL

2. **Limited LINQ support** - No aggregations, grouping, or complex joins in fluent API
   - Workaround: Use raw SQL for complex queries

3. **No lazy loading** - Must use `Include()` for navigation properties
   - Workaround: Use split queries and explicit includes

4. **No change tracking** - Manual change detection required
   - Workaround: Keep original values if needed for change detection

5. **Immediate execution** - No query batching like EF Core
   - Workaround: Use transactions for multiple operations

6. **No migrations** - DDL management is manual
   - Workaround: Use database migration tools (Flyway, DbUp, etc.)

7. **Oracle `InsertReturningIdSql`** - Limited support in v2.0
   - Workaround: Use sequences and retrieve ID separately

8. **No soft delete conventions** - Must implement manually
   - Workaround: Use `IsDeleted` flag and filter queries

9. **No automatic timestamp tracking** - Must set manually
   - Workaround: Use database triggers or set in code

10. **No query caching beyond expression compilation** - Each query executes
    - Workaround: Implement application-level caching (Redis, memory cache)

### Known Issues

1. **Singleton context detection** - May produce false positives in some scenarios
   - Impact: Warning logged after 1 minute if only 1-2 instances exist
   - Workaround: Ignore warnings if intentionally using singleton pattern (not recommended)

2. **Expression translation limitations** - Some complex LINQ patterns not supported
   - Impact: Runtime exception when unsupported pattern encountered
   - Workaround: Use raw SQL for complex expressions

3. **Case sensitivity** - Behavior varies by database and collation
   - Impact: Queries may behave differently on SQL Server vs Oracle
   - Workaround: Always use `ignoreCase: true` for cross-database compatibility

---

## Code Examples

### Complete CRUD Example

```csharp
public class CustomerRepository
{
    private readonly AppDbContext _db;

    public CustomerRepository(AppDbContext db)
    {
        _db = db;
    }

    // Create
    public async Task<int> CreateAsync(Customer customer)
    {
        return await _db.Customers.InsertAndGetIdAsync<int>(customer);
    }

    // Read
    public async Task<Customer?> GetByIdAsync(int id)
    {
        return await _db.Customers.FindAsync(id);
    }

    public async Task<List<Customer>> GetActiveAsync()
    {
        return (await _db.Customers.WhereAsync(c => c.IsActive)).ToList();
    }

    public async Task<List<Customer>> SearchAsync(string searchTerm)
    {
        return (await _db.Customers.WhereAsync(
            c => c.Name.Contains(searchTerm) ||
                 (c.Email != null && c.Email.Contains(searchTerm)),
            ignoreCase: true)).ToList();
    }

    // Update
    public async Task<bool> UpdateAsync(Customer customer)
    {
        var rows = await _db.Customers.UpdateAsync(customer);
        return rows > 0;
    }

    // Delete
    public async Task<bool> DeleteAsync(int id)
    {
        var rows = await _db.Customers.DeleteByIdAsync(id);
        return rows > 0;
    }

    // Pagination
    public async Task<PagedResult<Customer>> GetPagedAsync(int page, int pageSize)
    {
        var total = await _db.Customers.CountAsync(c => true);

        var items = await _db.Customers
            .Query()
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Customer>
        {
            Items = items,
            TotalCount = (int)total,
            Page = page,
            PageSize = pageSize
        };
    }
}
```

### Complex Query with Include

```csharp
public class OrderService
{
    private readonly AppDbContext _db;

    public OrderService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<OrderDto>> GetOrdersWithDetailsAsync(DateTime startDate)
    {
        var orders = await _db.Orders
            .Query()
            .Where(o => o.OrderDate >= startDate)
            .Include(o => o.Customer)
                .ThenInclude(c => c.Address)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
            .AsSplitQuery()
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        return orders.Select(o => new OrderDto
        {
            OrderId = o.Id,
            OrderDate = o.OrderDate,
            CustomerName = o.Customer?.Name,
            CustomerCity = o.Customer?.Address?.City,
            TotalAmount = o.Total,
            ItemCount = o.OrderItems?.Count ?? 0,
            Items = o.OrderItems?.Select(i => new OrderItemDto
            {
                ProductName = i.Product?.Name,
                Quantity = i.Quantity,
                Price = i.Price
            }).ToList()
        }).ToList();
    }
}
```

### Transaction Example

```csharp
public class OrderProcessor
{
    private readonly AppDbContext _db;

    public OrderProcessor(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> CreateOrderAsync(CreateOrderRequest request)
    {
        using var scope = await _db.BeginTransactionScopeAsync();
        try
        {
            // Create order
            var order = new Order
            {
                CustomerId = request.CustomerId,
                OrderDate = DateTime.UtcNow,
                Total = request.Items.Sum(i => i.Price * i.Quantity)
            };

            var orderId = await _db.Orders.InsertAndGetIdAsync<int>(order, scope.Transaction);

            // Create order items
            foreach (var item in request.Items)
            {
                var orderItem = new OrderItem
                {
                    OrderId = orderId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    Price = item.Price
                };

                await _db.OrderItems.InsertAsync(orderItem, scope.Transaction);

                // Update product stock
                var product = await _db.Products.FindAsync(item.ProductId);
                if (product == null)
                    throw new InvalidOperationException($"Product {item.ProductId} not found");

                product.StockQuantity -= item.Quantity;
                if (product.StockQuantity < 0)
                    throw new InvalidOperationException($"Insufficient stock for product {item.ProductId}");

                await _db.Products.UpdateAsync(product, scope.Transaction);
            }

            scope.Complete();
            return orderId;
        }
        catch
        {
            // Transaction automatically rolled back if Complete() not called
            throw;
        }
    }
}
```

### Validation Example

```csharp
public class CustomerService
{
    private readonly AppDbContext _db;

    public CustomerService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<int>> CreateCustomerAsync(Customer customer)
    {
        try
        {
            // Custom validation
            if (string.IsNullOrWhiteSpace(customer.Email))
                return Result<int>.Failure("Email is required");

            // Check for duplicate email
            var existing = await _db.Customers.FirstOrDefaultAsync(
                c => c.Email == customer.Email,
                ignoreCase: true);

            if (existing != null)
                return Result<int>.Failure("Email already exists");

            // Insert (built-in validation runs here)
            var id = await _db.Customers.InsertAndGetIdAsync<int>(customer);

            return Result<int>.Success(id);
        }
        catch (DapperValidationException ex)
        {
            var errors = string.Join(", ", ex.Errors.Select(e => e.ErrorMessage));
            return Result<int>.Failure($"Validation failed: {errors}");
        }
    }
}

public class Result<T>
{
    public bool IsSuccess { get; set; }
    public T? Value { get; set; }
    public string? Error { get; set; }

    public static Result<T> Success(T value) => new() { IsSuccess = true, Value = value };
    public static Result<T> Failure(string error) => new() { IsSuccess = false, Error = error };
}
```

---

## Summary

DapperForge is a lightweight, high-performance data access library that combines Dapper's speed with an Entity Framework-style API. It's designed for applications that need maximum query performance with minimal overhead while maintaining developer productivity through familiar patterns.

**Key Strengths:**
- Immediate execution (no change tracker)
- Minimal allocations and memory overhead
- Familiar EF-like API surface
- Pluggable SQL dialects
- Expression-to-SQL translation
- Automatic retry logic
- Transaction scoping
- Built-in validation

**Best For:**
- High-performance CRUD applications
- Microservices with simple data access
- Read-heavy workloads
- APIs requiring explicit SQL control

**Repository:** https://github.com/nahmadov/Nahmadov.DapperForge
**License:** MIT
**Version:** 2.0.0
**Target Framework:** .NET 10.0

---

*Last Updated: January 2026*
