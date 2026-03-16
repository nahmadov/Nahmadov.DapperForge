# Changelog

All notable changes to DapperForge are documented in this file.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

---

## [Unreleased]

### Added

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
