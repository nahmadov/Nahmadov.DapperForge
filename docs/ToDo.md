# DapperForge - ToDo

## Features

### Query API
- [ ] **`SelectAsync` / Projection support** — select specific columns or map to DTO types (e.g., `.Select(c => new { c.Id, c.Name }).ToListAsync()`). Currently always returns the full entity.
- [ ] **Aggregation methods** — `SumAsync`, `MinAsync`, `MaxAsync`, `AverageAsync` in both `DapperSet<T>` and `IDapperQueryable<T>`.
- [ ] **`GroupBy` support** — group results by a column and return aggregated projections via raw SQL generation.
- [ ] **`ToPagedListAsync`** — built-in pagination helper returning `{ Items, TotalCount, PageIndex, PageSize }` in a single round-trip (or two queries).
- [ ] **`IAsyncEnumerable<T>` streaming** — expose `ToAsyncEnumerable()` on `IDapperQueryable<T>` for large result sets using Dapper's `QueryUnbufferedAsync`.
- [ ] **`AllAsync` on `IDapperQueryable<T>`** — `AllAsync` is on `DapperSet<T>` but not exposed on the fluent `IDapperQueryable<T>` interface.
- [ ] **Case-insensitive `ignoreCase` on fluent API** — `Where(pred, ignoreCase: true)` only exists on `DapperSet.WhereAsync()`, not on `IDapperQueryable<T>.Where()`.
- [ ] **`WhereAsync` with `IN` clause from external variable** — `ids.Contains(c.Id)` is commented out in the sample runner; verify and fix evaluation of captured array/list variables in predicate expressions.

### Mutation API
- [ ] **`BulkDeleteAsync`** — delete multiple entities or rows matching a predicate in batched `DELETE ... WHERE ... IN (...)` statements.
- [ ] **`BulkUpdateAsync`** — update a specific set of columns across multiple rows in batches.
- [ ] **`InsertOrIgnoreAsync`** — insert a single entity, silently skip on duplicate key (dialect-specific: `INSERT OR IGNORE` / `INSERT IGNORE` / `ON CONFLICT DO NOTHING`).

### Entity Mapping
- [ ] **Composite primary key support** — `HasKey(e => new { e.TenantId, e.Id })`. Currently only single-column primary keys are supported; composite keys must be modelled as alternate keys with `HasAlternateKey`.
- [ ] **Owned / value-object support** — map nested value objects to columns of the same table (similar to EF Core `OwnsOne`).
- [ ] **Column default values** — `b.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()")` so that the column is excluded from INSERT and the DB default is used.
- [ ] **`HasComputedColumn`** — mark a column as `GENERATED ALWAYS AS (...)` so it is always excluded from INSERT and UPDATE.
- [ ] **`[NotMapped]` attribute support** — explicitly exclude a property from all SQL without marking it `IsReadOnly`.
- [ ] **Enum-to-string mapping** — optional convention to store enums as their string name instead of integer value.

### Dialect / Database Support
- [ ] **PostgreSQL dialect** — `Nahmadov.DapperForge.PostgreSql` package with `npgsql`, `$1` parameters, `"Identifier"` quoting, `RETURNING` for insert-returning-id, and `ON CONFLICT DO UPDATE` for merge.
- [ ] **MySQL / MariaDB dialect** — `` `Identifier` `` quoting, `?` or `@p0` parameters, `INSERT ... ON DUPLICATE KEY UPDATE` for merge, `LAST_INSERT_ID()` for identity.
- [ ] **SQLite dialect** — useful for testing and lightweight apps; `"Identifier"` quoting, `last_insert_rowid()` for identity.
- [ ] **SQL Server `NOLOCK` / query hints** — opt-in hint support via `.WithQueryHint("NOLOCK")` or `.AsNoLock()` on `IDapperQueryable<T>`.

### Developer Experience
- [ ] **Source-generated entity mapping** — optional Roslyn source generator that emits `IEntityTypeConfiguration<T>` classes and `SqlGenerator<T>` at compile time, eliminating reflection at startup.
- [ ] **Analyzer: unsupported LINQ patterns** — Roslyn diagnostic to warn at compile time when an expression uses patterns that `PredicateVisitor` cannot translate (e.g., `c.Name.ToLower()`, method calls on non-string types).
- [ ] **`IModelSnapshot` / model diff** — a lightweight snapshot of the resolved `EntityMapping` graph useful for tooling (schema doc generation, diff against live DB).

---

## Bugs / Known Issues

- [x] **`AsSplitQuery` modunda `Include`/`ThenInclude`-a yazılan `Where` şərtləri tətbiq edilmir** — split query strategiyasında hər navigation üçün ayrıca `SELECT ... WHERE Id IN (...)` sorğusu göndərilir; bu sorğulara root `Where` predikati ötürülmür. Nəticədə filtr yalnız root entity-yə tətbiq olunur, yüklənən əlaqəli entity-lər isə filtrsiz gəlir. `SplitIncludeLoader` / `IncludeFilterTranslator` tərəfindən düzəldilməlidir.
- [ ] **`ids.Contains(c.Id)` silently breaks with locally captured arrays** — in `SampleRunner.cs` (line 120-122) the IN-clause example is commented out. The `CollectionExpressionHandler` calls `ExpressionEvaluator.Evaluate()` on the outer variable; investigate whether array capture in a lambda closure is resolved correctly and add a regression test.
- [ ] **`IDapperQueryable<T>.ToListAsync()` returns `IEnumerable<T>` not `List<T>`** — the interface contract says `Task<IEnumerable<TEntity>>` while the docs and `DapperQueryable` implementation return `Task<List<TEntity>>`. Align the interface return type to `Task<List<TEntity>>` or update the docs.
- [ ] **`ignoreCase` wraps columns in `LOWER()`, defeating indexes** — consider generating a collation hint (`COLLATE SQL_Latin1_General_CP1_CI_AS`) for SQL Server and a function-based index note for Oracle instead of `LOWER()` wrapping.
- [ ] **Singleton detection false-positive on slow-start apps** — the 1-minute timer heuristic in `DapperDbContext.DetectSingletonAntiPattern()` fires a warning even for legitimately long-running single-request apps. Refine by tracking disposal events: if context is disposed before the timer fires, cancel the warning.
- [ ] **`DapperQueryable.ThenInclude` requires explicit generic type arguments** — callers must write `.ThenInclude<Employee, EmployeeAddress?>(emp => emp.Address)` because the compiler cannot infer `TPrevious`. Investigate whether the API can be redesigned (like EF Core's `IIncludableQueryable`) to allow type inference without explicit arguments.
- [ ] **`BulkMergeResult` missing `RowsInserted` / `RowsUpdated` for Oracle** — Oracle `MERGE` does not natively return split affected counts; the result always shows `RowsInserted = 0, RowsUpdated = 0`. Document the limitation or implement a workaround using `DML_RETURN_ROWCOUNT`.
- [ ] **`WhereAsync` on entity with inherited properties from `BaseEntity`** — verify that `PredicateVisitor` resolves column names correctly when the predicate references properties declared on a base class (e.g., `c => c.Name == "..."` where `Name` is on `BaseEntity`).

---

## Technical Debt / Improvements

- [ ] **Remove legacy `Connection` property and `BeginTransactionAsync()`** — both are already marked `[Obsolete]`. Schedule removal in the next major version (v3.0) and update the migration guide.
- [ ] **`GetSelectAllSqlFromGenerator` uses reflection** — `DapperDbContext.GetSelectAllSqlFromGenerator(object generator)` retrieves `SelectAllSql` via `PropertyInfo.GetValue`. Replace with a non-generic interface (e.g., `ISqlGeneratorCore`) that exposes `SelectAllSql` directly, eliminating the runtime reflection.
- [ ] **`DapperQueryable` ordering state uses `List<(string, bool)>`** — the ordering list stores raw column-name strings. Refactor to store typed `OrderingClause` records to avoid string coupling between `DapperQueryable` and `QuerySqlBuilder`.
- [ ] **Retry logic is only on queries, not mutations** — document clearly (already noted in Usage Guide) and consider making retry opt-in for idempotent mutations (e.g., `InsertAsync` with `allowRetry: true`).
- [ ] **`LruCache` maximum sizes are hardcoded constants** — expose `ExpressionCacheSize` (default 1000) and `IdentityCacheSize` (default 10 000) as configurable options in `DapperDbContextOptions`.
- [ ] **`IncludeTree` uses `List<IncludeNode>` for children** — traversal of deep ThenInclude chains may be O(n) per level; consider using a linked-list or dictionary keyed by property name for O(1) lookup.
- [ ] **`ContextConnectionManager` exponential backoff is fixed at 100/200/400 ms** — expose retry policy configuration (max retries, base delay, jitter) via `DapperDbContextOptions`.

---

## Testing

- [ ] **Integration test project** — add `Nahmadov.DapperForge.IntegrationTests` with a real SQL Server instance via Testcontainers; cover full CRUD, transactions, bulk ops, and include chains end-to-end.
- [ ] **Oracle integration tests** — mirror the SQL Server integration test suite against a real Oracle container using `gvenzl/oracle-xe`.
- [ ] **`ids.Contains(c.Id)` regression test** — unit test that exercises `WhereAsync(c => idList.Contains(c.Id))` with a captured local array to ensure the IN-clause is generated correctly.
- [ ] **Bulk operation integration tests** — test `BulkInsertAsync` and `BulkMergeAsync` with >2100 rows to validate batching boundary logic against a real database.
- [ ] **Concurrency / parallel query tests** — verify that two concurrent `IDapperQueryable` executions on the same context instance either serialize correctly or throw a clear error.
- [ ] **Performance benchmarks** — add a BenchmarkDotNet project comparing DapperForge vs raw Dapper vs EF Core for: single-row insert, bulk insert (1k rows), paginated read, include read.
- [ ] **`PredicateVisitor` fuzz tests** — property-based tests (FsCheck / CsCheck) that generate random LINQ expression trees and verify no unhandled exceptions are thrown (only `NotSupportedException` with a clear message).

---

## Documentation

- [x] **CHANGELOG.md** — create a changelog tracking breaking changes, new features, and bug fixes per release (start from v2.2.0 → current).
- [x] **NuGet package descriptions** — add `<Description>`, `<PackageTags>`, `<PackageReleaseNotes>` to each `.csproj`; update README badges for NuGet version and build status.
- [x] **PostgreSQL / MySQL usage guide** — add sections to `DapperForge-Usage-Guide.md` once the new dialect packages are published.
- [x] **"Migrating from EF Core" guide** — expand the brief table in README into a full step-by-step guide covering context setup, relationship configuration, query patterns, transaction handling, and known gaps.
- [x] **API XML docs completeness** — several public methods on `DapperSet<T>` and `DapperDbContext` are missing `<summary>` / `<param>` / `<returns>` tags; ensure full XML doc coverage for IntelliSense.
- [x] **Architecture diagram update** — the ASCII diagram in `DapperForge-Architecture.md` does not show the `BulkMutationExecutor` path; update to reflect the current layer structure.
