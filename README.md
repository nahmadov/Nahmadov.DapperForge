# DapperToolkit - Internal Technical Documentation

This document describes the internal architecture, current capabilities, limitations, and testing approach for DapperToolkit. It targets developers who maintain or extend the component.

---

## 1. Overview

- Lightweight data-access abstraction built on top of Dapper.
- EF-style surface with `DapperDbContext` and `DapperSet<TEntity>`.
- Reflection-based mapping with caching.
- SQL generation with pluggable dialects (SqlServer implemented, Oracle partial).
- Immediate execution (no change tracker).

---

## 2. Architecture

### 2.1 DapperDbContext
- Manages the connection lifecycle using a configured factory and dialect.
- Provides `QueryAsync`, `QueryFirstOrDefaultAsync`, `ExecuteAsync`, and `BeginTransactionAsync`.
- Caches `DapperSet<TEntity>` instances via `Set<TEntity>()`.

### 2.2 DapperSet<TEntity>
- EF-like API with immediate execution: `GetAllAsync`, `FindAsync`, `WhereAsync(Expression)`, `FirstOrDefaultAsync(sql, params)`, `InsertAsync`, `InsertAndGetIdAsync`, `UpdateAsync`, `DeleteAsync`, `DeleteByIdAsync`.
- Uses `SqlGenerator<TEntity>` to build SQL and `EntityValidator<TEntity>` for data annotations.

### 2.3 Mapping
- Reflection extracts table name (`[Table]`), schema, key (`[Key]`, `Id`, `{TypeName}Id`), and writable properties (ignores `[NotMapped]`).
- Column names from `[Column]`; identity/computed from `[DatabaseGenerated]`.
- Readonly entities via `[ReadOnlyEntity]`.
- Cached per entity (`EntityMappingCache<TEntity>`).

### 2.4 SQL Generation
- `SqlGenerator<TEntity>` produces SELECT/INSERT/UPDATE/DELETE SQL with quoted identifiers and parameter names from the dialect.
- Insert skips identity/computed columns; Update skips key/identity/computed.
- `InsertReturningIdSql` is generated only when the dialect supports it (SqlServer uses `SCOPE_IDENTITY`; Oracle currently returns null).

### 2.5 Dialects
- `ISqlDialect` defines parameter formatting, identifier quoting, boolean literals, and identity-return SQL.
- Provided implementations:
  - `SqlServerDialect` (InsertAndGetId supported).
  - `OracleDialect` (identity returning not implemented yet).

---

## 3. Usage

### 3.1 Registering DbContext
```csharp
services.AddDapperDbContext<AppDapperDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"));
});
```

### 3.2 Working With DbSet
```csharp
var user = await _db.Users.FindAsync(5);
var active = await _db.Users.WhereAsync(u => u.IsActive);

await _db.Users.InsertAsync(user);
await _db.Users.UpdateAsync(user);
await _db.Users.DeleteByIdAsync(5);
```

### 3.3 Thread Safety
- A single `DapperDbContext` instance must not be used concurrently. Create a scope per request (similar to EF Core).

### 3.4 Validation
- `[Required]` and `[StringLength]` are enforced on insert/update via `EntityValidator`.
- Readonly entities and missing keys are rejected before executing SQL.

---

## 4. Current Limitations

1. No change tracker or `SaveChanges`.
2. Only single-column primary keys.
3. Insert always includes the key unless marked identity/computed.
4. `[Column]` supports name but not schema override per property.
5. `WhereAsync` expression translator supports a small subset (comparisons, `Contains` on strings, boolean checks, null checks).
6. `FirstOrDefaultAsync(whereClause)` trusts caller SQL (potential injection if misused).
7. Oracle dialect does not implement identity/sequence returning; `InsertAndGetIdAsync` falls back to null.
8. No paging helpers, soft-delete conventions, or composite keys.
9. No logging/diagnostics hooks yet.

---

## 5. Testing

- Unit tests (xUnit) include:
  - Connection lifecycle and transaction behavior using fake ADO.NET primitives.
  - SQL generation for SqlServer and validation of identity skipping.
  - Predicate translation for expressions (booleans, null checks, contains, closures).
- Fakes live under `tests/DapperToolkit.UnitTests/Fakes` for DbConnection/Command/Transaction/Parameter.
- Run the suite with:
```
dotnet test
```

---

## 6. Design Principles

- Keep SQL explicit and predictable.
- Favor small, composable abstractions.
- Cache mapping and SQL generators for performance.
- Allow provider-specific behavior via dialects without touching core.
- Maintain a light surface area to stay close to Dapper performance.
