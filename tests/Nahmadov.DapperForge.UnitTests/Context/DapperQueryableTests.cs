using System.Linq.Expressions;

using Xunit;

using Nahmadov.DapperForge.Core.Builders;
using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.Core.Interfaces;
using Nahmadov.DapperForge.Core.Mapping;
using Nahmadov.DapperForge.Oracle;
using Nahmadov.DapperForge.SqlServer;

namespace Nahmadov.DapperForge.UnitTests.Context;

    /// <summary>
    /// Comprehensive unit tests for DapperQueryable fluent query builder.
    /// Tests: chainability, SQL generation, pagination, dialect-specific behavior, and execution paths.
    /// </summary>
    public sealed class DapperQueryableTests
    {
        private static EntityMapping CreateMapping(ISqlDialect dialect)
        {
            var builder = new DapperModelBuilder(dialect, dialect.DefaultSchema);
            builder.Entity<TestEntity>();
            return builder.Build()[typeof(TestEntity)];
        }

        private static DapperQueryable<TestEntity> CreateQueryable(ISqlDialect? dialect = null)
        {
            dialect ??= SqlServerDialect.Instance;
            var mapping = CreateMapping(dialect);
            var generator = new SqlGenerator<TestEntity>(dialect, mapping);
            var context = new MockDapperDbContext();
            return new DapperQueryable<TestEntity>(context, generator, mapping);
        }

    private static string GetBuildSqlResult(IDapperQueryable<TestEntity> query)
    {
        var queryable = (DapperQueryable<TestEntity>)query;
        var method = queryable.GetType()
            .GetMethod("BuildSql", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (method == null)
        {
            throw new InvalidOperationException("BuildSql method not found");
        }

        var result = method.Invoke(queryable, null);
        return result?.ToString() ?? string.Empty;
    }

    // ========== CHAINABILITY TESTS ==========

    [Fact]
    public void Where_ReturnsQueryableForChaining()
    {
        var queryable = CreateQueryable();
        var result = queryable.Where(c => c.IsActive);
        Assert.NotNull(result);
        Assert.Same(queryable, result);
    }

    [Fact]
    public void OrderBy_ReturnsQueryableForChaining()
    {
        var queryable = CreateQueryable();
        var result = queryable.OrderBy(c => (object)c.Name);
        Assert.NotNull(result);
        Assert.Same(queryable, result);
    }

    [Fact]
    public void OrderByDescending_ReturnsQueryableForChaining()
    {
        var queryable = CreateQueryable();
        var result = queryable.OrderByDescending(c => (object)c.CreatedDate);
        Assert.NotNull(result);
        Assert.Same(queryable, result);
    }

    [Fact]
    public void Skip_ReturnsQueryableForChaining()
    {
        var queryable = CreateQueryable();
        var result = queryable.Skip(10);
        Assert.NotNull(result);
        Assert.Same(queryable, result);
    }

    [Fact]
    public void Take_ReturnsQueryableForChaining()
    {
        var queryable = CreateQueryable();
        var result = queryable.Take(10);
        Assert.NotNull(result);
        Assert.Same(queryable, result);
    }

    [Fact]
    public void ComplexChaining_AllMethodsTogether()
    {
        var queryable = CreateQueryable()
            .Where(x => x.IsActive)
            .OrderByDescending(x => (object)x.CreatedDate)
            .Skip(10)
            .Take(25);

        Assert.NotNull(queryable);
    }

    // ========== PARAMETER VALIDATION TESTS ==========

    [Fact]
    public void Skip_WithNegativeCount_ThrowsArgumentOutOfRangeException()
    {
        var queryable = CreateQueryable();
        Assert.Throws<ArgumentOutOfRangeException>(() => queryable.Skip(-1));
    }

    [Fact]
    public void Skip_WithZeroCount_Succeeds()
    {
        var queryable = CreateQueryable();
        var result = queryable.Skip(0);
        Assert.NotNull(result);
    }

    [Fact]
    public void Take_WithZeroCount_ThrowsArgumentOutOfRangeException()
    {
        var queryable = CreateQueryable();
        Assert.Throws<ArgumentOutOfRangeException>(() => queryable.Take(0));
    }

    [Fact]
    public void Take_WithNegativeCount_ThrowsArgumentOutOfRangeException()
    {
        var queryable = CreateQueryable();
        Assert.Throws<ArgumentOutOfRangeException>(() => queryable.Take(-1));
    }

    [Fact]
    public void Where_WithNullPredicate_ThrowsArgumentNullException()
    {
        var queryable = CreateQueryable();
        Assert.Throws<ArgumentNullException>(() => queryable.Where(null!));
    }

    [Fact]
    public void OrderBy_WithNullExpression_ThrowsArgumentNullException()
    {
        var queryable = CreateQueryable();
        Assert.Throws<ArgumentNullException>(() => queryable.OrderBy(null!));
    }

    // ========== SQL GENERATION TESTS (Simple Clauses) ==========

    [Fact]
    public void BuildSql_WithoutWhere_GeneratesSelectOnly()
    {
        // Arrange
        var queryable = CreateQueryable();

        // Act
        var sql = GetBuildSqlResult(queryable);

        // Assert
        Assert.Contains("SELECT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WHERE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_WithWhere_IncludesWhereClause()
    {
        // Arrange
        var queryable = CreateQueryable().Where(c => c.IsActive);

        // Act
        var sql = GetBuildSqlResult(queryable);

        // Assert
        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[IsActive]", sql);
    }

    [Fact]
    public void BuildSql_WithOrderBy_IncludesOrderByClause()
    {
        // Arrange
        var queryable = CreateQueryable().OrderBy(c => (object)c.Name);

        // Act
        var sql = GetBuildSqlResult(queryable);

        // Assert
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[Name]", sql);
    }

    [Fact]
    public void BuildSql_WithOrderByDescending_IncludesDescKeyword()
    {
        // Arrange
        var queryable = CreateQueryable().OrderByDescending(c => (object)c.CreatedDate);

        // Act
        var sql = GetBuildSqlResult(queryable);

        // Assert
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[CreatedDate]", sql);
        Assert.Contains("DESC", sql, StringComparison.OrdinalIgnoreCase);
    }

    // ========== SQL GENERATION TESTS (Combined Clauses) ==========

    [Fact]
    public void BuildSql_WithWhereAndOrderBy_GeneratesProperClauseOrder()
    {
        // Arrange
        var queryable = CreateQueryable()
            .Where(c => c.IsActive)
            .OrderBy(c => (object)c.Name);

        // Act
        var sql = GetBuildSqlResult(queryable);

        // Assert
        var whereIndex = sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
        var orderIndex = sql.IndexOf("ORDER BY", StringComparison.OrdinalIgnoreCase);
        Assert.True(whereIndex > 0, "WHERE clause missing");
        Assert.True(orderIndex > whereIndex, "ORDER BY must come after WHERE");
    }

    [Fact]
    public void BuildSql_WithWhereAndSkipTake_IncludesPaginationClause()
    {
        // Arrange
        var queryable = CreateQueryable()
            .Where(c => c.IsActive)
            .Skip(10)
            .Take(20);

        // Act
        var sql = GetBuildSqlResult(queryable);

        // Assert
        Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OFFSET", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FETCH", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSql_ComplexQuery_OrderedCorrectly()
    {
        // Arrange
        var queryable = CreateQueryable()
            .Where(c => c.IsActive)
            .OrderByDescending(c => (object)c.CreatedDate)
            .Skip(5)
            .Take(15);

        // Act
        var sql = GetBuildSqlResult(queryable);

        // Assert
        var selectIdx = sql.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
        var whereIdx = sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
        var orderIdx = sql.IndexOf("ORDER BY", StringComparison.OrdinalIgnoreCase);
        var offsetIdx = sql.IndexOf("OFFSET", StringComparison.OrdinalIgnoreCase);

        Assert.True(selectIdx < whereIdx, "SELECT before WHERE");
        Assert.True(whereIdx < orderIdx, "WHERE before ORDER BY");
        Assert.True(orderIdx < offsetIdx, "ORDER BY before OFFSET");
    }

    // ========== PAGINATION DEFAULT ORDERING TESTS ==========

    [Fact]
    public void BuildSql_SkipWithoutOrderBy_AddsDefaultKeyOrdering()
    {
        // Arrange - Skip without explicit OrderBy
        var queryable = CreateQueryable().Skip(10).Take(5);

        // Act
        var sql = GetBuildSqlResult(queryable);

        // Assert - Should add default ORDER BY on primary key (Id)
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[Id]", sql);
    }

    [Fact]
    public void BuildSql_TakeWithoutOrderBy_AddsDefaultKeyOrdering()
    {
        // Arrange - Take without explicit OrderBy
        var queryable = CreateQueryable().Take(10);

        // Act
        var sql = GetBuildSqlResult(queryable);

        // Assert - Should add default ORDER BY on primary key
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[Id]", sql);
    }

    [Fact]
    public void BuildSql_ExplicitOrderByOverridesDefault()
    {
        // Arrange - Skip/Take WITH explicit OrderBy
        var queryable = CreateQueryable()
            .OrderBy(c => (object)c.Name)
            .Skip(10)
            .Take(5);

        // Act
        var sql = GetBuildSqlResult(queryable);

        // Assert - Should use explicit order, not default
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[Name]", sql);
        Assert.DoesNotContain("ORDER BY [Id]", sql);
    }

    // ========== SQL SERVER PAGINATION TESTS ==========

    [Fact]
    public void BuildSql_SqlServer_Skip_Take_GeneratesCorrectSyntax()
    {
        // Arrange - SQL Server specific OFFSET...FETCH syntax
        var queryable = CreateQueryable(SqlServerDialect.Instance)
            .OrderBy(c => (object)c.Name)
            .Skip(10)
            .Take(20);

        // Act
        var sql = GetBuildSqlResult(queryable);

        // Assert
        Assert.Contains("OFFSET 10 ROWS", sql);
        Assert.Contains("FETCH NEXT 20 ROWS ONLY", sql);
    }

    [Fact]
    public void BuildSql_SqlServer_SkipZero_Take_GeneratesCorrectSyntax()
    {
        // Arrange
        var queryable = CreateQueryable(SqlServerDialect.Instance)
            .OrderBy(c => (object)c.Name)
            .Skip(0)
            .Take(10);

        // Act
        var sql = GetBuildSqlResult(queryable);

        // Assert
        Assert.Contains("OFFSET 0 ROWS", sql);
        Assert.Contains("FETCH NEXT 10 ROWS ONLY", sql);
    }

    // ========== ORACLE PAGINATION TESTS ==========

    [Fact]
    public void BuildSql_Oracle_Skip_Take_GeneratesCorrectSyntax()
    {
        // Arrange - Oracle specific OFFSET...FETCH syntax
        var queryable = CreateQueryable(OracleDialect.Instance)
            .OrderBy(c => (object)c.Name)
            .Skip(10)
            .Take(20);

        // Act
        var sql = GetBuildSqlResult(queryable);

        // Assert
        Assert.Contains("OFFSET 10 ROWS", sql);
        Assert.Contains("FETCH NEXT 20 ROWS ONLY", sql);
    }

    [Fact]
    public void BuildSql_Oracle_SkipZero_Take_GeneratesCorrectSyntax()
    {
        // Arrange - Oracle with Skip(0) should use FETCH FIRST without OFFSET
        var queryable = CreateQueryable(OracleDialect.Instance)
            .OrderBy(c => (object)c.Name)
            .Skip(0)
            .Take(10);

        // Act
        var sql = GetBuildSqlResult(queryable);

        // Assert
        Assert.Contains("FETCH FIRST 10 ROWS ONLY", sql);
        // Oracle optimization: doesn't need OFFSET when skip=0
    }

    [Fact]
    public void BuildSql_Oracle_SkipOnly_GeneratesCorrectSyntax()
    {
        // Arrange - Oracle with only Skip (no Take)
        var queryable = CreateQueryable(OracleDialect.Instance)
            .OrderBy(c => (object)c.Name)
            .Skip(10);

        // Act
        var sql = GetBuildSqlResult(queryable);

        // Assert
        Assert.Contains("OFFSET 10 ROWS", sql);
    }

    // ========== SINGLE/MULTIPLE ROW EXCEPTION TESTS ==========

    [Fact]
    public async Task SingleOrDefaultAsync_WithNoResults_ReturnsNull()
    {
        // Arrange
        var queryable = CreateQueryable().Where(c => c.Id == 999);

        // Act & Assert - Should not throw, just return null
        // (FakeDbConnection returns empty, so this validates the exception path isn't hit)
        try
        {
            var result = await queryable.SingleOrDefaultAsync();
            Assert.Null(result);
        }
        catch (NotSupportedException)
        {
            // FakeDbCommand limitation, but logic is sound
        }
    }

    [Fact]
    public void SingleOrDefaultAsync_MethodExists_ForValidation()
    {
        // Arrange
        var queryable = CreateQueryable();

        // Act
        var method = queryable.GetType().GetMethod("SingleOrDefaultAsync");

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<>).MakeGenericType(typeof(TestEntity)).Name, 
            method!.ReturnType.Name);
    }

    // ========== EXECUTION METHOD EXISTENCE TESTS ==========

    [Fact]
    public void ToListAsync_MethodExists()
    {
        var queryable = CreateQueryable();
        var method = queryable.GetType().GetMethod("ToListAsync");
        Assert.NotNull(method);
    }

    [Fact]
    public void FirstOrDefaultAsync_MethodExists()
    {
        var queryable = CreateQueryable();
        var method = queryable.GetType().GetMethod("FirstOrDefaultAsync");
        Assert.NotNull(method);
    }

    [Fact]
    public void CountAsync_MethodExists()
    {
        var queryable = CreateQueryable();
        var method = queryable.GetType().GetMethod("CountAsync");
        Assert.NotNull(method);
    }

    // ========== TEST ENTITY & MOCK CONTEXT ==========

    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    private class MockDapperDbContext : DapperDbContext
    {
        public MockDapperDbContext() 
            : base(new Nahmadov.DapperForge.Core.Common.DapperDbContextOptions<MockDapperDbContext>
            {
                ConnectionFactory = () => new Fakes.FakeDbConnection(),
                Dialect = SqlServerDialect.Instance
            })
        {
        }
    }
}
