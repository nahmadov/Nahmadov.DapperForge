# DapperForge - Architecture Documentation

## Overview

DapperForge is built on a layered architecture that separates concerns and provides extensibility points for different database systems.

## Architecture Layers

```
┌─────────────────────────────────────────┐
│        Application Layer                │
│  (Controllers, Services, etc.)          │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│        DapperDbContext                  │
│  (Connection Management, Entity Sets)   │
└─────────────────┬───────────────────────┘
                  │
    ┌─────────────┼─────────────┐
    │             │             │
┌───▼───┐   ┌────▼────┐   ┌───▼────┐
│ Query │   │Mutation │   │ Config │
│ Layer │   │  Layer  │   │ Layer  │
└───┬───┘   └────┬────┘   └───┬────┘
    │            │            │
┌───▼────────────▼────────────▼────┐
│      SQL Generation Layer        │
│  (SqlGenerator, PredicateVisitor)│
└──────────────┬───────────────────┘
               │
┌──────────────▼───────────────────┐
│      SQL Dialect Layer           │
│  (SqlServer, Oracle, etc.)       │
└──────────────┬───────────────────┘
               │
┌──────────────▼───────────────────┐
│         Dapper + ADO.NET         │
│         Database                 │
└──────────────────────────────────┘
```

## Core Components

### 1. Context Layer

#### DapperDbContext

**Location:** [DapperDbContext.cs](../src/Nahmadov.DapperForge.Core/Context/DapperDbContext.cs)

**Purpose:** Base class for all database contexts, acts as gateway to database operations.

**Key Responsibilities:**
- Connection lifecycle management
- Entity set management via `Set<TEntity>()`
- Transaction and connection scope management
- Low-level Dapper wrappers
- Singleton detection

**Key APIs:**
```csharp
public abstract class DapperDbContext : IDisposable
{
    // Entity Sets
    protected DapperSet<TEntity> Set<TEntity>();

    // Low-level Dapper Wrappers
    protected Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param);
    protected Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param);
    protected Task<int> ExecuteAsync(string sql, object? param);

    // Connection/Transaction Management
    public IConnectionScope CreateConnectionScope();
    public Task<ITransactionScope> BeginTransactionScopeAsync();

    // Configuration
    protected virtual void OnModelCreating(DapperModelBuilder modelBuilder);
}
```

#### DapperSet<TEntity>

**Location:** [DapperSet.cs](../src/Nahmadov.DapperForge.Core/Context/DapperSet.cs)

**Purpose:** Provides query and command operations for a specific entity type.

**Design Pattern:** Facade pattern - delegates to specialized executors:
- `EntityQueryExecutor<TEntity>` - Handles all query operations
- `EntityMutationExecutor<TEntity>` - Handles all insert/update/delete operations

**Key APIs:**
```csharp
public class DapperSet<TEntity>
{
    // Query Operations
    Task<IEnumerable<TEntity>> GetAllAsync();
    Task<TEntity?> FindAsync(object key);
    Task<IEnumerable<TEntity>> WhereAsync(Expression<Func<TEntity, bool>> predicate);
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate);
    Task<long> CountAsync(Expression<Func<TEntity, bool>> predicate);
    IDapperQueryable<TEntity> Query();

    // Mutation Operations
    Task<int> InsertAsync(TEntity entity, IDbTransaction? transaction = null);
    Task<int> UpdateAsync(TEntity entity, IDbTransaction? transaction = null);
    Task<int> DeleteAsync(TEntity entity, IDbTransaction? transaction = null);
    Task<TKey> InsertAndGetIdAsync<TKey>(TEntity entity, IDbTransaction? transaction = null);
}
```

### 2. Query System

#### Query State Management

**Components:**
- `QueryState<TEntity>` - Stores predicates, ordering, skip/take
- `IncludeTree` - Models Include/ThenInclude relationships
- `QuerySplittingBehavior` - Single vs Split query strategy
- `IdentityCache` - Prevents duplicate entity instances

#### IDapperQueryable<TEntity>

**Location:** [IDapperQueryable.cs](../src/Nahmadov.DapperForge.Core/Interfaces/IDapperQueryable.cs)

**Purpose:** Define fluent query builder contract.

```csharp
public interface IDapperQueryable<TEntity>
{
    // Filtering
    IDapperQueryable<TEntity> Where(Expression<Func<TEntity, bool>> predicate);

    // Ordering
    IDapperQueryable<TEntity> OrderBy(Expression<Func<TEntity, object?>> keySelector);
    IDapperQueryable<TEntity> OrderByDescending(Expression<Func<TEntity, object?>> keySelector);

    // Pagination
    IDapperQueryable<TEntity> Skip(int count);
    IDapperQueryable<TEntity> Take(int count);

    // Distinct
    IDapperQueryable<TEntity> Distinct();

    // Include Navigation
    IIncludableQueryable<TEntity, TProperty> Include<TProperty>(
        Expression<Func<TEntity, TProperty>> navigationPropertyPath);

    // Query Splitting
    IDapperQueryable<TEntity> AsSplitQuery();
    IDapperQueryable<TEntity> AsSingleQuery();
    IDapperQueryable<TEntity> AsNoIdentityResolution();

    // Execution
    Task<List<TEntity>> ToListAsync();
    Task<TEntity> FirstAsync();
    Task<TEntity?> FirstOrDefaultAsync();
    Task<bool> AnyAsync();
    Task<long> CountAsync();
}
```

#### Query Execution Flow

```
User calls Query()
    ↓
DapperQueryable<TEntity> created
    ↓
User chains Where/OrderBy/Include/etc
    ↓
User calls ToListAsync()
    ↓
QueryExecutionCoordinator determines strategy:
    ├─ No includes → ExecuteSimpleQueryAsync()
    ├─ Includes + SingleQuery → ExecuteSingleQueryWithIncludesAsync()
    └─ Includes + SplitQuery → ExecuteSplitQueryWithIncludesAsync()
    ↓
SQL generated via SqlGenerator + PredicateVisitor
    ↓
Dapper executes query
    ↓
Results mapped and relationships populated
    ↓
Returns List<TEntity>
```

### 3. Expression Translation System

#### PredicateVisitor<TEntity>

**Location:** [PredicateVisitor.cs](../src/Nahmadov.DapperForge.Core/Builders/PredicateVisitor.cs)

**Purpose:** Translate LINQ expression trees to SQL WHERE clauses.

**Supported Patterns:**
- Comparisons: `==`, `!=`, `>`, `>=`, `<`, `<=`
- Null checks: `== null`, `!= null`
- Boolean properties: `u.IsActive`, `!u.IsActive`
- String methods: `Contains()`, `StartsWith()`, `EndsWith()`
- Collection Contains: `ids.Contains(u.Id)` → `IN` clause
- Logical operators: `&&`, `||`, `!`
- Case-insensitive comparisons

**Performance Feature:**
- Thread-safe LRU cache (max 1000 entries)
- Expression structural hashing for cache keys
- Compiled expressions cached and reused

**Example Translation:**
```csharp
// LINQ Expression
u => u.IsActive && u.Name.StartsWith("John") && u.Age > 18

// Generated SQL
WHERE a.[IsActive] = 1 AND a.[Name] LIKE @p0 AND a.[Age] > @Age

// Parameters
{ "@p0": "John%", "@Age": 18 }
```

### 4. SQL Generation System

#### SqlGenerator<TEntity>

**Location:** [SqlGenerator.cs](../src/Nahmadov.DapperForge.Core/Builders/SqlGenerator.cs)

**Purpose:** Generate parameterized SQL for CRUD operations.

**Generated SQL Statements:**
```csharp
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

**Generation Logic:**
1. Builds full table name with schema and quoting
2. Identifies key columns (primary OR alternate)
3. Separates insertable vs updatable properties:
   - **Insertable:** NOT generated (except sequences) AND NOT read-only
   - **Updatable:** NOT key, NOT generated, NOT read-only
4. Generates parameterized SQL using dialect-specific formatting

**Example Output (SQL Server):**
```sql
-- SelectAllSql
SELECT a.[Id], a.[Name], a.[Email], a.[CreatedAt]
FROM [dbo].[Users] AS a

-- InsertReturningIdSql
INSERT INTO [dbo].[Users] ([Name], [Email])
VALUES (@Name, @Email);
SELECT CAST(SCOPE_IDENTITY() AS int)

-- UpdateSql
UPDATE [dbo].[Users]
SET [Name] = @Name, [Email] = @Email
WHERE [Id] = @Id

-- DeleteByIdSql
DELETE FROM [dbo].[Users] WHERE [Id] = @Id
```

### 5. Mapping System

#### Entity Mapping

**Key Classes:**
- `EntityMapping` - Immutable mapping metadata
- `PropertyMapping` - Property-level configuration
- `ForeignKeyMapping` - Foreign key relationships
- `EntityConfig` - Builder-time configuration
- `EntityMappingResolver` - Combines attributes + fluent config
- `EntityMappingCache` - Caches resolved mappings

**EntityMapping Structure:**
```csharp
public class EntityMapping
{
    public Type EntityType { get; }
    public string TableName { get; }
    public string? Schema { get; }
    public IReadOnlyList<PropertyInfo> KeyProperties { get; }
    public IReadOnlyList<PropertyInfo> AlternateKeyProperties { get; }
    public IReadOnlyList<PropertyMapping> PropertyMappings { get; }
    public IReadOnlyList<ForeignKeyMapping> ForeignKeys { get; }
    public bool IsReadOnly { get; }
}
```

**PropertyMapping Structure:**
```csharp
public class PropertyMapping
{
    public PropertyInfo Property { get; }
    public string ColumnName { get; }
    public DatabaseGeneratedOption? GeneratedOption { get; }
    public bool IsReadOnly { get; }
    public bool IsRequired { get; }
    public int? MaxLength { get; }
    public string? SequenceName { get; }

    // Computed properties
    public bool IsIdentity => GeneratedOption == DatabaseGeneratedOption.Identity;
    public bool IsComputed => GeneratedOption == DatabaseGeneratedOption.Computed;
    public bool IsGenerated => IsIdentity || IsComputed || IsReadOnly || UsesSequence;
}
```

**Mapping Resolution Flow:**
```
Application starts
    ↓
DapperDbContext constructor called
    ↓
ContextModelManager.Initialize()
    ↓
OnModelCreating(modelBuilder)
    ↓
modelBuilder.Entity<T>() configurations
    ↓
modelBuilder.Build()
    ↓
EntityMappingResolver combines:
    - Attribute metadata ([Table], [Key], [Column], [Required], etc.)
    - Fluent configuration (ToTable, HasKey, Property().IsRequired(), etc.)
    ↓
Returns IReadOnlyDictionary<Type, EntityMapping>
    ↓
Cached for context lifetime
```

### 6. SQL Dialect System

#### ISqlDialect

**Location:** [ISqlDialect.cs](../src/Nahmadov.DapperForge.Core/Interfaces/ISqlDialect.cs)

**Purpose:** Abstract database-specific SQL syntax.

```csharp
public interface ISqlDialect
{
    string Name { get; }
    string? DefaultSchema { get; }
    string FormatParameter(string baseName);
    string QuoteIdentifier(string identifier);
    string FormatTableAlias(string alias);
    string BuildInsertReturningId(string baseInsertSql, string tableName, params string[] keyColumnNames);
    string FormatBoolean(bool value);
    bool TryMapDbType(Type clrType, out DbType dbType);
}
```

#### Dialect Implementations

**SqlServerDialect:**
```csharp
FormatParameter("p0")          → "@p0"
QuoteIdentifier("Id")          → "[Id]"
FormatTableAlias("a")          → "AS a"
FormatBoolean(true)            → "1"
DefaultSchema                  → "dbo"
BuildInsertReturningId(...)    → "INSERT ... SELECT CAST(SCOPE_IDENTITY() AS int)"
```

**OracleDialect:**
```csharp
FormatParameter("p0")          → ":p0"
QuoteIdentifier("Id")          → "\"Id\""
FormatTableAlias("a")          → "a"
FormatBoolean(true)            → "1"
DefaultSchema                  → null
BuildInsertReturningId(...)    → "INSERT ... RETURNING column INTO :param"
```

### 7. Connection & Transaction Management

#### ConnectionScope

**Location:** [ConnectionScope.cs](../src/Nahmadov.DapperForge.Core/Context/Connection/ConnectionScope.cs)

**Purpose:** Scoped database connection with automatic lifecycle management.

**Lifecycle:**
1. Connection created lazily on first access
2. Connection opened if not already open
3. Connection health verified before use
4. Connection closed and returned to pool on Dispose()

#### TransactionScope

**Location:** [TransactionScope.cs](../src/Nahmadov.DapperForge.Core/Context/Connection/TransactionScope.cs)

**Purpose:** Scoped database transaction with automatic commit/rollback.

**Usage Pattern:**
```csharp
var transaction = await context.BeginTransactionScopeAsync();
try
{
    await context.Users.InsertAsync(user, transaction.Transaction);
    transaction.Complete(); // Mark for commit
}
finally
{
    transaction.Dispose(); // Auto-commits if Complete() called, else rolls back
}
```

#### ContextConnectionManager

**Location:** [ContextConnectionManager.cs](../src/Nahmadov.DapperForge.Core/Context/Connection/ContextConnectionManager.cs)

**Key Responsibilities:**
- Create and dispose connection scopes
- Track active transactions
- Ensure only one active transaction per context
- Implement exponential backoff retry logic
- Health checking via `SELECT 1` query

### 8. Validation System

#### EntityValidator<TEntity>

**Location:** [EntityValidator.cs](../src/Nahmadov.DapperForge.Core/Validation/EntityValidator.cs)

**Purpose:** Validate entities before insert/update operations.

**Validation Rules:**
1. **Read-Only Check** - Throws if entity is read-only
2. **Required Fields** - From `[Required]` or `.IsRequired()`
3. **String Length** - From `[StringLength]`, `[MaxLength]` or `.HasMaxLength()`
4. **Minimum Length** - From `[StringLength(min, max)]`

**Exceptions:**
- `DapperValidationException` - Lists all validation errors
- `DapperReadOnlyException` - Entity is read-only

### 9. Include/ThenInclude System

#### IncludeTree & IncludeNode

**Location:** [IncludeTree.cs](../src/Nahmadov.DapperForge.Core/Query/IncludeTree.cs)

**Purpose:** Model hierarchical Include/ThenInclude relationships.

```csharp
public class IncludeTree
{
    IReadOnlyList<IncludeNode> Roots { get; }
    IncludeNode AddRoot(PropertyInfo navigation, Type relatedType, bool isCollection);
}

public class IncludeNode
{
    PropertyInfo Navigation { get; }
    Type RelatedType { get; }
    bool IsCollection { get; }
    IReadOnlyList<IncludeNode> Children { get; }
}
```

#### Query Execution Strategies

**1. AsSingleQuery (Default for simple includes):**
- Single SQL query with JOINs
- Potential cartesian product with collections
- Better for single or small result sets

**2. AsSplitQuery (Recommended for collections):**
- Multiple SQL queries with IN clauses
- Avoids cartesian product
- Better performance for large result sets

**Example:**
```csharp
// Single Query
SELECT o.*, c.*, a.*
FROM Orders o
LEFT JOIN Customers c ON o.CustomerId = c.Id
LEFT JOIN Addresses a ON c.AddressId = a.Id

// Split Query
SELECT * FROM Orders
SELECT * FROM Customers WHERE Id IN (@id1, @id2, @id3)
SELECT * FROM Addresses WHERE CustomerId IN (@cid1, @cid2)
```

### 10. Caching System

#### Expression Compilation Cache

**Location:** [PredicateVisitor.cs](../src/Nahmadov.DapperForge.Core/Builders/PredicateVisitor.cs)

- LRU cache with max 1000 entries
- Structural expression hashing for cache keys
- Thread-safe concurrent access
- Avoids recompilation of common predicates

#### SQL Statement Cache

**Location:** [SqlGeneratorProvider.cs](../src/Nahmadov.DapperForge.Core/Context/Utilities/SqlGeneratorProvider.cs)

- One `SqlGenerator<TEntity>` per entity type
- All SQL statements pre-generated at initialization
- Cached for context lifetime
- Eliminates runtime SQL generation overhead

#### Identity Cache

**Location:** [IdentityCache.cs](../src/Nahmadov.DapperForge.Core/Query/IdentityCache.cs)

- LRU cache (max 10,000 entities per query)
- Prevents duplicate entity instances during Include operations
- Disposed after query completion
- Reduces memory consumption

## Design Patterns Used

1. **Repository Pattern** - `DapperSet<TEntity>` acts as repository
2. **Unit of Work** - `TransactionScope` manages transactions
3. **Builder Pattern** - `DapperModelBuilder`, `EntityTypeBuilder`, `PropertyBuilder`
4. **Strategy Pattern** - `ISqlDialect` implementations
5. **Facade Pattern** - `DapperSet` facades query/mutation executors
6. **Factory Pattern** - Connection factory in options
7. **Template Method** - `OnModelCreating()` hook
8. **Immutable Object** - `EntityMapping`, `PropertyMapping`

## Extension Points

1. **Custom SQL Dialects** - Implement `ISqlDialect`
2. **Custom Entity Configurations** - Implement `IEntityTypeConfiguration<T>`
3. **Connection Factory** - Custom connection creation logic
4. **Logging** - Custom `ILogger` implementation

## Thread Safety

- **DapperDbContext** - NOT thread-safe (use scoped lifetime)
- **Expression Cache** - Thread-safe (ConcurrentDictionary)
- **SQL Generator Cache** - Thread-safe (ImmutableDictionary)
- **IdentityCache** - NOT thread-safe (per-query scope)

## Memory Management

- **No Change Tracker** - Minimal memory overhead
- **LRU Caches** - Bounded memory usage
- **Connection Pooling** - ADO.NET connection pooling
- **Lazy Initialization** - Connections created on first use
- **Disposable Pattern** - Proper resource cleanup

## Performance Optimizations

1. **Expression Compilation Caching** - Avoids repeated compilation
2. **SQL Statement Pre-generation** - No runtime SQL generation
3. **Parameterized Queries** - Database query plan caching
4. **Connection Pooling** - Reuse database connections
5. **Identity Resolution** - Prevents duplicate entity instances
6. **Split Query Strategy** - Avoids cartesian products
7. **Retry Logic** - Handles transient failures without user intervention

## Limitations

1. **Single-column keys only** - No composite key support
2. **Limited LINQ support** - No aggregations, grouping, complex joins in fluent API
3. **No lazy loading** - Must use Include
4. **No change tracking** - Manual change detection required
5. **Immediate execution** - No query batching
6. **No migrations** - Manual DDL management

## Next Steps

- [Usage Guide](./DapperForge-Usage-Guide.md) - Comprehensive usage examples
- [Complete Reference](./DapperForge-Complete-Reference.md) - Full API reference
