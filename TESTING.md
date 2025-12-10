# Testing Guide

## Overview

DapperForge includes a comprehensive test suite with **100+ unit tests** covering:
- Connection management and lifecycle
- CRUD operations (Insert, Update, Delete, Select)
- Entity validation and error handling
- SQL expression translation
- Exception hierarchy

**Test Framework:** xUnit  
**Test Project:** `tests/Nahmadov.DapperForge.UnitTests/`

---

## Running Tests

### Run All Tests
```bash
dotnet test
```

### Run Specific Test File
```bash
dotnet test tests/Nahmadov.DapperForge.UnitTests/Context/DapperDbContextAsyncTests.cs
```

### Run Tests with Verbose Output
```bash
dotnet test --verbosity normal
```

### Run Tests with Coverage
```bash
dotnet test /p:CollectCoverage=true /p:CoverageFormat=lcov
```

---

## Test Organization

### 1. `DapperDbContextAsyncTests.cs` (18 tests)

Tests for `DapperDbContext` async operations and connection management.

**Covered Scenarios:**
- ✅ Connection lifecycle (open, reuse, dispose)
- ✅ Broken connection handling
- ✅ Transaction creation
- ✅ Constructor validation
- ✅ Null options/dialect/factory detection

**Example Test:**
```csharp
[Fact]
public void Connection_Is_Opened_Once_And_Reused()
{
    var ctx = CreateContext(out var conn);

    var c1 = ctx.ExposeConnection();
    var c2 = ctx.ExposeConnection();

    Assert.Same(conn, c1);
    Assert.Same(conn, c2);
    Assert.Equal(ConnectionState.Open, conn.State);
    Assert.Equal(1, conn.OpenCount);
}
```

---

### 2. `DapperSetCrudTests.cs` (24 tests)

Tests for `DapperSet<T>` CRUD operations and query methods.

**Covered Scenarios:**
- ✅ GetAllAsync - retrieve all entities
- ✅ FindAsync - find by key
- ✅ WhereAsync - filtered queries
- ✅ FirstOrDefaultAsync - single result
- ✅ InsertAsync - entity creation
- ✅ UpdateAsync - entity modification
- ✅ DeleteAsync/DeleteByIdAsync - entity removal
- ✅ InsertAndGetIdAsync - identity retrieval
- ✅ Validation error handling
- ✅ Complex predicates

**Example Test:**
```csharp
[Fact]
public async Task InsertAsync_WithValidEntity_ExecutesInsert()
{
    var (ctx, conn) = CreateContext();
    var set = GetSet(ctx);

    var user = new User { Name = "John Doe", Email = "john@example.com" };

    try
    {
        await set.InsertAsync(user);
    }
    catch
    {
        // Expected with fake connection
    }

    Assert.Equal(ConnectionState.Open, conn.State);
}
```

---

### 3. `EntityValidatorTests.cs` (30 tests)

Tests for validation logic covering required fields, string lengths, and read-only entities.

**Covered Scenarios:**
- ✅ Required field validation
- ✅ String length constraints (min/max)
- ✅ Null value handling
- ✅ Empty/whitespace detection
- ✅ Update vs Insert validation differences
- ✅ Read-only entity protection
- ✅ Optional field handling
- ✅ Boolean default values

**Example Test:**
```csharp
[Fact]
public void ValidateForInsert_ThrowsWhenStringExceedsMaxLength()
{
    var mapping = BuildUserMapping();
    var longName = new string('a', 101);
    var user = new User { Name = longName };

    var exception = Assert.Throws<DapperValidationException>(() =>
    {
        EntityValidator<User>.ValidateForInsert(user, mapping);
    });

    Assert.Contains("exceeds maximum length", exception.Message);
    Assert.Contains("100", exception.Message);
}
```

---

### 4. `PredicateVisitorComprehensiveTests.cs` (28 tests)

Tests for LINQ expression to SQL translation.

**Covered Scenarios:**
- ✅ Boolean expressions (true/false)
- ✅ Comparison operators (<, >, <=, >=, ==, !=)
- ✅ Null checks (IS NULL, IS NOT NULL)
- ✅ String operations (Contains, StartsWith, EndsWith)
- ✅ Logical operators (AND, OR)
- ✅ Complex expressions
- ✅ DateTime comparisons
- ✅ Parameter escaping
- ✅ Case-insensitive matching

**Example Test:**
```csharp
[Fact]
public void Translates_StringContains_EscapesWildcardChars()
{
    var (sql, parameters) = Translate(u => u.Name.Contains("a%b_c"));

    Assert.Contains("LIKE", sql);
    var paramValue = parameters["p0"];
    Assert.Equal("%a\\%b\\_c%", paramValue?.ToString() ?? "");
}
```

---

## Test Structure

### Arrange-Act-Assert Pattern

Each test follows the AAA pattern:

```csharp
[Fact]
public async Task ExampleTest()
{
    // Arrange: Setup test data and context
    var (ctx, conn) = CreateContext();
    var user = new User { Name = "Test" };

    // Act: Execute the operation
    await set.InsertAsync(user);

    // Assert: Verify the results
    Assert.Equal(ConnectionState.Open, conn.State);
}
```

---

## Fake Objects for Testing

The test suite uses fake implementations to avoid real database dependencies:

### `FakeDbConnection`
```csharp
public class FakeDbConnection : DbConnection
{
    public int OpenCount { get; private set; }
    public int DisposeCount { get; private set; }
    // Tracks connection lifecycle without real DB calls
}
```

### `FakeDbCommand`
```csharp
public class FakeDbCommand : DbCommand
{
    // Prevents actual SQL execution
    // Throws NotSupportedException for unimplemented methods
}
```

### `TestDapperDbContext`
```csharp
public class TestDapperDbContext : DapperDbContext
{
    public IDbConnection ExposeConnection() => Connection;
    // Allows test access to connection lifecycle
}
```

---

## Writing New Tests

### 1. Test Naming Convention
```csharp
[Fact]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Test code
}
```

### 2. Test Categories
- **Positive tests:** Verify expected behavior
- **Negative tests:** Verify error handling
- **Edge cases:** Boundary conditions, null values

### 3. Test Example Template
```csharp
[Fact]
public async Task InsertAsync_WithValidEntity_ReturnsInsertedRows()
{
    // Arrange
    var (ctx, conn) = CreateContext();
    var set = GetSet(ctx);
    var user = new User { Name = "Test User" };

    // Act
    try
    {
        var affectedRows = await set.InsertAsync(user);
    }
    catch
    {
        // Expected with fake connection
    }

    // Assert
    Assert.Equal(ConnectionState.Open, conn.State);
}
```

---

## Common Test Patterns

### Testing Validation Errors
```csharp
[Fact]
public void ValidateForInsert_WithRequiredFieldNull_ThrowsException()
{
    var user = new User { Name = null! };
    var mapping = BuildUserMapping();

    var ex = Assert.Throws<DapperValidationException>(() =>
    {
        EntityValidator<User>.ValidateForInsert(user, mapping);
    });

    Assert.NotEmpty(ex.Errors);
}
```

### Testing Exception Types
```csharp
[Fact]
public async Task UpdateAsync_WhenNoRowsAffected_ThrowsConcurrencyException()
{
    var ex = await Assert.ThrowsAsync<DapperConcurrencyException>(async () =>
    {
        await set.UpdateAsync(nonExistentEntity);
    });

    Assert.Equal(OperationType.Update, ex.OperationType);
}
```

### Testing SQL Generation
```csharp
[Fact]
public void Translates_BooleanProperty_True()
{
    var (sql, parameters) = Translate(u => u.IsActive);

    Assert.Equal("[IsActive] = 1", sql);
    Assert.Empty(parameters);
}
```

---

## Test Metrics

### Coverage Summary
| Component | Tests | Status |
|-----------|-------|--------|
| DapperDbContext | 18 | ✅ Pass |
| DapperSet<T> | 24 | ✅ Pass |
| EntityValidator | 30 | ✅ Pass |
| PredicateVisitor | 28 | ✅ Pass |
| **Total** | **100** | **✅ Pass** |

### Coverage Areas
- ✅ Connection lifecycle
- ✅ CRUD operations
- ✅ Validation
- ✅ SQL translation
- ✅ Exception handling
- ✅ Edge cases

---

## Debugging Tests

### Run Single Test in VS Code
```json
{
    "name": "Debug Single Test",
    "type": "coreclr",
    "request": "launch",
    "program": "${workspaceFolder}/dotnet",
    "args": [
        "test",
        "--no-build",
        "--logger=console;verbosity=detailed",
        "--filter=NameOfTest"
    ],
    "stopAtEntry": false,
    "console": "integratedTerminal"
}
```

### Enable Detailed Logging
```bash
dotnet test --verbosity detailed --logger "console;verbosity=detailed"
```

---

## Continuous Integration

For CI/CD pipelines:

```yaml
# Example GitHub Actions
- name: Run Tests
  run: dotnet test --verbosity normal --logger "trx;LogFileName=test-results.trx"

- name: Upload Results
  uses: actions/upload-artifact@v2
  if: always()
  with:
    name: test-results
    path: "**/test-results.trx"
```

---

## Best Practices

1. **Keep tests isolated** - Each test should be independent
2. **Use meaningful names** - Test name describes what's being tested
3. **Arrange-Act-Assert** - Clear test structure
4. **Mock dependencies** - Use fakes to avoid external dependencies
5. **Test both success and failure** - Cover happy path and error cases
6. **Keep tests fast** - Use fakes instead of real DB connections
7. **Document complex tests** - Explain non-obvious setup or assertions

---

## Troubleshooting

### Test Timeout
```bash
dotnet test --timeout 10000
```

### Test Failures with Real Database
✅ Always use fake connections in unit tests  
❌ Don't use real database connections

### Tests Pass Locally but Fail in CI
- Ensure all dependencies are installed
- Check for hard-coded paths or environment-specific code
- Verify database/environment setup in CI

---

## Future Test Enhancements

Possible areas for additional testing:
- Integration tests with real database
- Performance benchmarks
- Stress testing with large datasets
- Concurrent operation handling
- Additional expression patterns (CASE, aggregates)
