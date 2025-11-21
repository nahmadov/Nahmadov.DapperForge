# DapperToolkit — Internal Technical Documentation

This document describes the internal architecture, usage conventions, limitations, and planned improvements for the **DapperToolkit** module.  
It is intended for developers who maintain or extend this component inside the project.

---

## 1. Introduction

DapperToolkit is a lightweight data-access abstraction built on top of Dapper.  
It aims to provide an EF‑like developer experience while preserving Dapper-level performance and transparency.

The core design avoids heavy abstractions such as LINQ providers or complex ORMs.  
Instead, the toolkit focuses on:

- A minimal **DbContext** implementation  
- EF-style **DbSet<TEntity>**  
- Automatic SQL generation for common CRUD operations  
- Reflection-based entity mapping  
- Provider-based infrastructure for database connections  
- Clean extensibility for future ChangeTracker or advanced SQL builders  

---

## 2. Architecture Overview

The architecture consists of several layers working together.  
Each layer is isolated, cached where necessary, and designed for extensibility.

---

### 2.1. DapperDbContext Layer

`DapperDbContext` is the main entry point for all operations.

Responsibilities:

- Creates and opens connections from provider factories  
- Manages connection lifecycle  
- Ensures safe disposal  
- Executes Dapper commands  
- Provides transactions  
- Creates and caches `DapperSet<TEntity>` instances via `Set<TEntity>()`

Example inheritance:

```csharp
public class AppDapperDbContext : DapperDbContext
{
    public AppDapperDbContext(DapperDbContextOptions<AppDapperDbContext> options)
        : base(options) { }

    public DapperSet<User> Users => Set<User>();
}
```

---

### 2.2. DapperSet<TEntity> Layer

`DapperSet<TEntity>` behaves similarly to EF Core’s `DbSet<TEntity>` but uses **immediate execution**.

Supported operations:

- `GetAllAsync()`  
- `FindAsync(id)`  
- `QueryAsync("WHERE ...")`  
- `FirstOrDefaultAsync("WHERE ...")`  
- `InsertAsync(entity)`  
- `UpdateAsync(entity)`  
- `DeleteAsync(entity)`  
- `DeleteByIdAsync(id)`  

All SQL is generated based on entity metadata and executed immediately.  
There is no ChangeTracker or SaveChanges at this stage.

---

### 2.3. Entity Mapping Layer

The reflection-based metadata system extracts:

- Table name (from `[Table]` or class name)  
- Primary key (from `[Key]`, or conventions: `Id`, `{TypeName}Id`)  
- Column names (currently equal to property names)  
- List of public writable properties  

This metadata is cached per entity type for performance.  
Mapping is used by SQL generator and DapperSet.

---

### 2.4. SQL Generation Layer

SQL generation is handled by a cached generator per entity type.

Generated SQL:

- `SELECT * FROM Table`  
- `SELECT ... WHERE Key = @Id`  
- `INSERT INTO Table (...) VALUES (...)`  
- `UPDATE Table SET ... WHERE Key = @Id`  
- `DELETE FROM Table WHERE Key = @Id`  

The generator currently assumes SQL Server syntax (`@ParamName`).  
Dialect-specific support is planned.

---

### 2.5. Provider Layer

Providers create physical database connections.

Current providers:

- `DapperToolkit.SqlServer`  
- `DapperToolkit.Oracle`  

Example registration:

```csharp
services.AddDapperDbContext<AppDapperDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});
```

This design isolates database-specific behavior from the core library.

---

## 3. Usage Guidelines

### 3.1. Registering DbContext

```csharp
builder.Services.AddDapperDbContext<AppDapperDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});
```

### 3.2. Working With DbSet

```csharp
var user = await _db.Users.FindAsync(5);
var list = await _db.Users.QueryAsync("WHERE IsActive = 1");

await _db.Users.InsertAsync(user);
await _db.Users.UpdateAsync(user);
await _db.Users.DeleteByIdAsync(5);
```

### 3.3. Thread Safety

A single DbContext instance must **not** be used concurrently.  
Just like EF Core, create a new scope per request.

---

## 4. Current Limitations

The implementation is functional but intentionally minimal.

1. **No ChangeTracker**  
2. **Primary key always included in INSERT**  
3. **No SQL dialect abstraction**  
4. **No `[Column]` or schema support**  
5. **Unsafe raw SQL in QueryAsync**  
6. **No soft delete convention**  
7. **No paging methods**  
8. **Only single-column primary keys**  
9. **Readonly entities not supported**  
10. **Validation attributes ignored**  

---

## 5. Planned Enhancements

Future improvements include:

- Full `[Column]` & `[Table(Schema="..")]` mapping  
- SQL Dialect layer (SqlServer, Oracle, PostgreSQL, MySQL)  
- Identity/Sequence handling  
- Safe Query Builder  
- Composite key support  
- Soft delete (`ISoftDeletable`)  
- Paging API (`GetPageAsync`)  
- Logging & diagnostics  
- Readonly DbSet mode  
- ChangeTracker layer with SaveChangesAsync  
- Provider-specific SQL features  

The architecture allows adding these without breaking existing APIs.

---

## 6. Testing

The toolkit includes fake connection classes for unit testing.

Unit tests validate:

- Connection reuse  
- Broken connection recovery  
- Transaction behavior  
- SQL generation correctness  

No real database is required.

---

## 7. Development Notes

Design principles:

- No heavy abstractions  
- SQL is explicit and predictable  
- Mapping and SQL generation are cached  
- Providers extend the system without modifying core  
- DbSet and DbContext remain lightweight  
- Clean extensibility for ChangeTracker  

---

## 8. Conclusion

DapperToolkit offers a structured yet lightweight ORM-like experience built on Dapper.  
Its current foundation is stable and extensible, enabling future enhancements such as:

- SQL Dialects  
- Safe querying  
- More advanced mapping options  
- Change tracking  

All extensions must maintain the core principles: **simplicity, performance, transparency**.
