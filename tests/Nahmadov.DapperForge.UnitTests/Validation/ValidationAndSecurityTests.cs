using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Nahmadov.DapperForge.Core.Builders;
using Nahmadov.DapperForge.Core.Common;
using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.Core.Exceptions;
using Nahmadov.DapperForge.Core.Mapping;
using Nahmadov.DapperForge.SqlServer;
using Nahmadov.DapperForge.UnitTests.Fakes;

using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Validation;

/// <summary>
/// Tests for validation, null handling, empty key handling, and SQL injection prevention.
/// </summary>
public class ValidationAndSecurityTests
{
    [Table("Users")]
    private class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Email { get; set; }
    }

    [Table("Products")]
    private class Product
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public Guid ProductId { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public decimal Price { get; set; }
    }

    private static (TestDapperDbContext ctx, FakeDbConnection conn) CreateContext()
    {
        var conn = new FakeDbConnection();
        var options = new DapperDbContextOptions<TestDapperDbContext>
        {
            ConnectionFactory = () => conn,
            Dialect = SqlServerDialect.Instance
        };

        return (new TestDapperDbContext(options), conn);
    }

    private static DapperSet<User> GetUserSet(TestDapperDbContext ctx)
    {
        var builder = new DapperModelBuilder(SqlServerDialect.Instance);
        builder.Entity<User>();
        var model = builder.Build();
        var mapping = model[typeof(User)];
        var generator = new SqlGenerator<User>(SqlServerDialect.Instance, mapping);
        return new DapperSet<User>(ctx, generator, mapping);
    }

    private static DapperSet<Product> GetProductSet(TestDapperDbContext ctx)
    {
        var builder = new DapperModelBuilder(SqlServerDialect.Instance);
        builder.Entity<Product>();
        var model = builder.Build();
        var mapping = model[typeof(Product)];
        var generator = new SqlGenerator<Product>(SqlServerDialect.Instance, mapping);
        return new DapperSet<Product>(ctx, generator, mapping);
    }

    private class TestDapperDbContext : DapperDbContext
    {
        public TestDapperDbContext(DapperDbContextOptions options) : base(options) { }

        public DapperSet<User> Users { get; set; } = null!;
        public DapperSet<Product> Products { get; set; } = null!;
    }

    #region Null Entity Validation Tests

    [Fact]
    public async Task InsertAsync_NullEntity_ThrowsArgumentNullException()
    {
        var (ctx, _) = CreateContext();
        var set = GetUserSet(ctx);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await set.InsertAsync(null!));
    }

    [Fact]
    public async Task UpdateAsync_NullEntity_ThrowsArgumentNullException()
    {
        var (ctx, _) = CreateContext();
        var set = GetUserSet(ctx);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await set.UpdateAsync(null!));
    }

    [Fact]
    public async Task DeleteAsync_NullEntity_ThrowsArgumentNullException()
    {
        var (ctx, _) = CreateContext();
        var set = GetUserSet(ctx);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await set.DeleteAsync(null!));
    }

    [Fact]
    public async Task InsertAndGetIdAsync_NullEntity_ThrowsArgumentNullException()
    {
        var (ctx, _) = CreateContext();
        var set = GetUserSet(ctx);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await set.InsertAndGetIdAsync<int>(null!));
    }

    [Fact]
    public async Task DeleteByIdAsync_NullKey_ThrowsArgumentNullException()
    {
        var (ctx, _) = CreateContext();
        var set = GetUserSet(ctx);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await set.DeleteByIdAsync(null!));
    }

    #endregion

    #region SQL Injection Prevention Tests

    [Fact]
    public void WhereAsync_SqlInjectionInStringValue_GeneratesParameterizedSql()
    {
        var maliciousInput = "admin'; DROP TABLE Users; --";

        var builder = new DapperModelBuilder(SqlServerDialect.Instance);
        builder.Entity<User>();
        var model = builder.Build();
        var mapping = model[typeof(User)];

        var visitor = new PredicateVisitor<User>(mapping, SqlServerDialect.Instance);
        var (sql, parameters) = visitor.Translate(u => u.Name == maliciousInput);

        // Verify it uses parameters, not string concat
        Assert.Contains("@p", sql);
        Assert.DoesNotContain("DROP TABLE", sql);
        Assert.DoesNotContain(maliciousInput, sql);

        // Verify parameter contains the malicious value safely
        var dict = parameters as Dictionary<string, object?>;
        Assert.NotNull(dict);
        Assert.Contains(dict.Values, v => v?.ToString() == maliciousInput);
    }

    [Fact]
    public void WhereAsync_SqlInjectionInLikePattern_EscapesSpecialCharacters()
    {
        var maliciousPattern = "%' OR '1'='1";

        var builder = new DapperModelBuilder(SqlServerDialect.Instance);
        builder.Entity<User>();
        var model = builder.Build();
        var mapping = model[typeof(User)];

        var visitor = new PredicateVisitor<User>(mapping, SqlServerDialect.Instance);
        var (sql, parameters) = visitor.Translate(u => u.Name.Contains(maliciousPattern));

        // Should use LIKE with ESCAPE
        Assert.Contains("LIKE", sql);
        Assert.Contains("ESCAPE", sql);
        Assert.Contains("@p", sql);

        // SQL should not contain the injection attempt
        Assert.DoesNotContain("OR '1'='1", sql);
    }

    [Fact]
    public void FirstOrDefaultAsync_PredicateWithMaliciousInput_UsesParameters()
    {
        var maliciousEmail = "test@test.com' OR 1=1 --";

        var builder = new DapperModelBuilder(SqlServerDialect.Instance);
        builder.Entity<User>();
        var model = builder.Build();
        var mapping = model[typeof(User)];

        var visitor = new PredicateVisitor<User>(mapping, SqlServerDialect.Instance);
        var (sql, _) = visitor.Translate(u => u.Email == maliciousEmail);

        // Should be parameterized
        Assert.Contains("@p", sql);
        Assert.DoesNotContain("OR 1=1", sql);
        Assert.DoesNotContain(maliciousEmail, sql);
    }

    #endregion

    #region Required Field Validation Tests

    [Fact]
    public async Task InsertAsync_RequiredFieldNull_ThrowsValidationException()
    {
        var (ctx, _) = CreateContext();
        var set = GetUserSet(ctx);
        var user = new User { Name = null! }; // Required field is null

        var ex = await Assert.ThrowsAsync<DapperValidationException>(async () =>
            await set.InsertAsync(user));

        Assert.Contains("Name", ex.Message);
        Assert.Contains("required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAsync_RequiredFieldNull_ThrowsValidationException()
    {
        var (ctx, _) = CreateContext();
        var set = GetUserSet(ctx);
        var user = new User { Id = 1, Name = null! }; // Required field is null

        var ex = await Assert.ThrowsAsync<DapperValidationException>(async () =>
            await set.UpdateAsync(user));

        Assert.Contains("Name", ex.Message);
        Assert.Contains("required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Predicate Null Handling Tests

    [Fact]
    public async Task WhereAsync_PredicateNull_ThrowsArgumentNullException()
    {
        var (ctx, _) = CreateContext();
        var set = GetUserSet(ctx);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await set.WhereAsync(null!));
    }

    [Fact]
    public async Task FirstOrDefaultAsync_PredicateNull_ThrowsArgumentNullException()
    {
        var (ctx, _) = CreateContext();
        var set = GetUserSet(ctx);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await set.FirstOrDefaultAsync(null!));
    }

    #endregion
}
