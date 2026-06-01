# Changelog

All notable changes to DapperForge are documented in this file.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

---

## [2.5.0] — 2026-06-01

### Added

- **Enum-based SQL column type system** — DapperForge now knows a column's SQL *type*, not just its name. New dialect-agnostic `SqlColumnType` enum and immutable `ColumnTypeFacets` (length/precision/scale/nullability), automatic CLR→`SqlColumnType` inference (e.g. `int→Int`, `decimal→Decimal`, `DateTime→DateTime2`, `string→NVarChar`, `byte[]→VarBinary`, `Nullable<T>`→underlying + nullable), and `PropertyBuilder.HasColumnType(...)` overrides. Each dialect resolves the enum to concrete DDL via `ISqlDialect.GetColumnTypeSql(...)` — SQL Server emits real types (`nvarchar(50)`, `decimal(18,4)`, `datetime2`, …), SQLite emits storage affinities (`INTEGER`/`REAL`/`NUMERIC`/`TEXT`/`BLOB`). Other dialects throw `NotSupportedException`.
- **Fluent session temp-table builder** — `context.TempTable("name").Column<int>(...).Column("Amount", SqlColumnType.Decimal, ...).CreateAsync(connection)` creates a session temp table without raw DDL, driven by the same column-type system. SQL Server emits `CREATE TABLE #Name ( … )` (ensuring the `#` prefix); SQLite emits `CREATE TEMP TABLE "Name" ( … )`. `BuildCreateTableSql()` returns the DDL without executing. Unsupported dialects surface `NotSupportedException` / `ISqlDialect.SupportsSessionTempTables == false`.
- **Create temp table from a `DataTable`** — `context.CreateTempTableFromAsync(name, dataTable, connection)` derives a session temp table directly from an ADO.NET `DataTable`: each `DataColumn` maps to a column with the SQL type inferred from `DataType`, nullability from `AllowDBNull`, and string length from `MaxLength`. The natural source when the caller already holds a `DataTable` for bulk copy.
- **Create temp table mirroring an entity** — `db.Set<T>().CreateTempTableLikeAsync(name, connection)` (and the convenience `context.CreateTempTableLikeAsync<T>(name, connection)`) create a session temp table from a mapped entity, using the resolved column names (respecting `HasColumnName`) and types. Database-generated / identity columns are excluded by default; an optional `params` overload selects a column subset (and includes explicitly chosen generated columns).
- **Bulk copy** — first-class bulk insert that derives column mappings from the entity mapping, replacing hand-written `SqlBulkCopy` + `ColumnMappings` plumbing. `db.Set<T>().BulkCopyAsync(rows, destinationTable, connection, options?)` (entity-driven) and `context.BulkCopyAsync(dataTable, destinationTable, connection, options?)` (DataTable-driven). **SQL Server** uses `SqlBulkCopy` (in `Nahmadov.DapperForge.SqlServer`, never referenced from Core); **SQLite** falls back to batched, parameterized multi-row `INSERT` inside a single transaction, respecting the 999-parameter limit. `BulkCopyOptions` exposes `BatchSize`, `TimeoutSeconds`, `EnableStreaming`, and `UseTableLock` (SqlBulkCopy-specific options ignored by SQLite). Because the temp-table DDL and bulk-copy column mappings share the same `EntityMapping`, `CreateTempTableLikeAsync<T>` + `BulkCopyAsync(rows, "#tmp")` compose cleanly.
- **SQLite dialect** (`Nahmadov.DapperForge.Sqlite`) — new provider package backed by `Microsoft.Data.Sqlite`. Supports `"identifier"` quoting, `@param` placeholders, `last_insert_rowid()` for identity retrieval, and automatic IN-clause batching (max 999 parameters per statement).

### Changed

- **Unified expression translator** — all SQL predicate translation is now handled by a single, non-generic `SqlPredicateTranslator` class that is configurable by table alias and parameter prefix. `PredicateVisitor<TEntity>` is now a thin typed wrapper delegating to `SqlPredicateTranslator`. Deleted `IncludeFilterTranslator` and `EntityPropertyHelper` (both superseded).

### Fixed

- **`AsSplitQuery` Include filter bug** — `Where` conditions passed inside `Include`/`ThenInclude` were silently ignored when using split-query strategy. `SplitIncludeLoader` now applies the filter expression as a parameterised SQL `AND` clause on every generated `SELECT … WHERE … IN (…)` statement.

---

## [2.2.0] — 2026-02-09

### Added

- **`BulkInsertAsync`** — inserts a large collection of entities in a single batched statement. Automatically splits oversized batches to respect per-dialect parameter limits (SQL Server: 2100, Oracle: 1000, SQLite: 999).
- **`BulkMergeAsync`** — upsert (insert-or-update) large sets of entities using dialect-specific MERGE / INSERT OR REPLACE / ON CONFLICT semantics.
- **`Include` / `ThenInclude` filter support** — pass a `Where` predicate directly inside an Include call to filter the loaded related entities at the SQL level.

### Fixed

- `assignNavigations` bug in single-query Include strategy that caused navigation properties to be assigned to the wrong parent instances.
- Column-splitting bug in single-query Include strategy that produced incorrect `splitOn` column lists.
- Transaction scope connection handling bug causing sporadic `InvalidOperationException` when a transaction was committed or rolled back.

---

## [2.1.3] — 2026-01-16

### Fixed

- `PropertyInfo` equality comparison for properties declared on base classes and interfaces. Previously, `PredicateVisitor` could throw "no mapping found" when a predicate referenced an inherited property (e.g., `c => c.Name == "x"` where `Name` is on `BaseEntity`).

---

## [2.1.2] — 2026-01-16

### Changed

- Reorganised `Nahmadov.DapperForge.Core` into a feature-based folder structure (`Querying`, `Modeling`, `Context`, `Infrastructure`); updated all internal namespaces accordingly.
- Refactored large test classes into helper partials for improved maintainability.
- Refactored `PredicateVisitor` internals for cleaner handler separation.

---

## [2.1.1] — 2026-01-15

### Added

- Composite alternate key support via `HasAlternateKey(e => new { e.TenantId, e.Code })`.

### Changed

- Refactored `EntityMutationExecutor` for clarity and reduced complexity.

### Fixed

- Connection scoping stability; `IN`-clause evaluation with captured local variables in predicate expressions (`ids.Contains(c.Id)`) now resolved correctly.

---

## [2.1.0] — 2026-01-15

Initial public release baseline on .NET 10.

### Added

- `DapperDbContext` — abstract base class providing Dapper-based data access with entity mapping, connection lifecycle management, and singleton detection.
- `DapperSet<TEntity>` — typed entry point for CRUD operations (`InsertAsync`, `UpdateAsync`, `DeleteAsync`, `FindAsync`, `GetAllAsync`, `WhereAsync`, `FirstOrDefaultAsync`, `AnyAsync`, `CountAsync`).
- `IDapperQueryable<TEntity>` fluent query API — `Where`, `OrderBy`, `ThenBy`, `Skip`, `Take`, `Include`, `ThenInclude`, `AsSplitQuery`, `UseIdentityResolution`, `ToListAsync`, `FirstOrDefaultAsync`, `CountAsync`.
- Fluent entity configuration via `DapperModelBuilder` — `HasKey`, `HasAlternateKey`, `ToTable`, `HasSchema`, `HasForeignKey`, `Property().HasColumnName()`, `ApplyConfigurationsFromAssembly`.
- `ISqlDialect` / `IBulkSqlDialect` — extensibility interfaces for database-specific SQL syntax.
- `SqlServer` and `Oracle` provider packages.
- Transaction scope API — `BeginTransactionScopeAsync`, `ITransactionScope` with commit / rollback.
- Expression caching (thread-safe LRU, max 1 000 entries).
- SQL logging via `Microsoft.Extensions.Logging` or console fallback.

---

## [2.0.0] — 2026-01-07

- Upgraded target framework to .NET 10.
- Initial versioned NuGet release.

---

## [1.1.0] — 2026-01-07

- Early preview release.
