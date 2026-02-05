using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Builders;
using Nahmadov.DapperForge.Core.Mutations.Bulk;
using Nahmadov.DapperForge.SqlServer;

using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Bulk;

public class BulkSqlBuilderTests
{
    #region Batch Size Calculation Tests

    [Fact]
    public void CalculateOptimalBatchSize_RequestedZero_ReturnsAutoCalculated()
    {
        var builder = CreateBuilder<EntityWithIdentity>();

        // EntityWithIdentity has 1 insertable column (Name) - Id is identity
        // Max params = 2100, so max rows = 2100 / 1 = 2100
        // But capped at 500 for auto
        var batchSize = builder.CalculateOptimalBatchSize(0);

        Assert.Equal(500, batchSize);
    }

    [Fact]
    public void CalculateOptimalBatchSize_RequestedWithinLimit_ReturnsRequested()
    {
        var builder = CreateBuilder<EntityWithIdentity>();

        var batchSize = builder.CalculateOptimalBatchSize(100);

        Assert.Equal(100, batchSize);
    }

    [Fact]
    public void CalculateOptimalBatchSize_RequestedExceedsLimit_ReturnsCapped()
    {
        var builder = CreateBuilder<EntityWithIdentity>();

        // EntityWithIdentity has 1 insertable column
        // Max rows = 2100 / 1 = 2100
        // Requested is 3000, so should be capped at 2100
        var batchSize = builder.CalculateOptimalBatchSize(3000);

        Assert.Equal(2100, batchSize);
    }

    [Fact]
    public void CalculateOptimalBatchSize_ManyColumns_ReturnsSmallBatch()
    {
        var builder = CreateBuilder<ManyColumnsEntity>();

        // ManyColumnsEntity has 19 insertable columns (Col1 is key, not identity)
        // Max rows = 2100 / 19 = 110
        var batchSize = builder.CalculateOptimalBatchSize(0);

        Assert.Equal(110, batchSize);
    }

    #endregion

    #region Bulk Insert SQL Generation Tests

    [Fact]
    public void BuildBulkInsertSql_SingleEntity_GeneratesCorrectSql()
    {
        var builder = CreateBuilder<EntityWithIdentity>();
        var entities = new List<EntityWithIdentity>
        {
            new() { Name = "Test" }
        };

        var (sql, parameters) = builder.BuildBulkInsertSql(entities);

        Assert.Contains("INSERT INTO", sql);
        Assert.Contains("[Name]", sql);
        Assert.Contains("@Name0", sql);
        Assert.DoesNotContain("[Id]", sql); // Id is identity, should be excluded
    }

    [Fact]
    public void BuildBulkInsertSql_MultipleEntities_GeneratesMultipleValueRows()
    {
        var builder = CreateBuilder<EntityWithIdentity>();
        var entities = new List<EntityWithIdentity>
        {
            new() { Name = "User1" },
            new() { Name = "User2" },
            new() { Name = "User3" }
        };

        var (sql, parameters) = builder.BuildBulkInsertSql(entities);

        Assert.Contains("@Name0", sql);
        Assert.Contains("@Name1", sql);
        Assert.Contains("@Name2", sql);
    }

    [Fact]
    public void BuildBulkInsertSql_ExcludesIdentityColumn()
    {
        var builder = CreateBuilder<EntityWithIdentity>();
        var entities = new List<EntityWithIdentity>
        {
            new() { Name = "Test" }
        };

        var (sql, parameters) = builder.BuildBulkInsertSql(entities);

        Assert.DoesNotContain("[Id]", sql);
        Assert.Contains("[Name]", sql);
    }

    #endregion

    #region Parameter Flattening Tests

    [Fact]
    public void BuildBulkInsertSql_ParametersAreFlattened()
    {
        var builder = CreateBuilder<EntityWithIdentity>();
        var entities = new List<EntityWithIdentity>
        {
            new() { Name = "User1" },
            new() { Name = "User2" }
        };

        var (sql, parameters) = builder.BuildBulkInsertSql(entities);

        // Verify parameters are flattened with index suffix
        Assert.Equal("User1", parameters.Get<string>("Name0"));
        Assert.Equal("User2", parameters.Get<string>("Name1"));
    }

    [Fact]
    public void BuildBulkInsertSql_WithMultipleColumns_ParametersAreFlattened()
    {
        var builder = CreateBuilder<TwoColumnEntity>();
        var entities = new List<TwoColumnEntity>
        {
            new() { Name = "User1", Email = "user1@example.com" },
            new() { Name = "User2", Email = null }
        };

        var (sql, parameters) = builder.BuildBulkInsertSql(entities);

        Assert.Equal("User1", parameters.Get<string>("Name0"));
        Assert.Equal("user1@example.com", parameters.Get<string>("Email0"));
        Assert.Equal("User2", parameters.Get<string>("Name1"));
        Assert.Null(parameters.Get<string?>("Email1"));
    }

    #endregion

    #region Merge SQL Generation Tests

    [Fact]
    public void BuildMergeSql_WithPrimaryKey_GeneratesCorrectOnClause()
    {
        var builder = CreateBuilder<EntityWithIdentity>();
        var entities = new List<EntityWithIdentity>
        {
            new() { Id = 1, Name = "Test" }
        };

        var options = new BulkMergeOptions();
        var (sql, parameters) = builder.BuildMergeSql(entities, options);

        Assert.Contains("MERGE INTO", sql);
        Assert.Contains("ON target.[Id] = source.[Id]", sql);
    }

    [Fact]
    public void BuildMergeSql_WithCustomMatchColumns_UsesSpecifiedColumns()
    {
        var builder = CreateBuilder<TwoColumnEntity>();
        var entities = new List<TwoColumnEntity>
        {
            new() { Name = "Test", Email = "test@example.com" }
        };

        var options = new BulkMergeOptions
        {
            MatchColumns = new[] { "Email" }
        };
        var (sql, parameters) = builder.BuildMergeSql(entities, options);

        Assert.Contains("ON target.[Email] = source.[Email]", sql);
    }

    [Fact]
    public void BuildMergeSql_InsertOnly_OmitsUpdateClause()
    {
        var builder = CreateBuilder<EntityWithIdentity>();
        var entities = new List<EntityWithIdentity>
        {
            new() { Id = 1, Name = "Test" }
        };

        var options = new BulkMergeOptions { Mode = MergeMode.InsertOnly };
        var (sql, parameters) = builder.BuildMergeSql(entities, options);

        Assert.Contains("WHEN NOT MATCHED THEN", sql);
        Assert.DoesNotContain("WHEN MATCHED THEN", sql);
    }

    #endregion

    #region Helper Methods

    private static BulkSqlBuilder<T> CreateBuilder<T>() where T : class
    {
        var dialect = SqlServerDialect.Instance;
        var modelBuilder = new DapperModelBuilder(dialect);
        modelBuilder.Entity<T>();
        var mapping = modelBuilder.Build()[typeof(T)];

        return new BulkSqlBuilder<T>((IBulkSqlDialect)dialect, mapping);
    }

    #endregion

    #region Test Entities

    [Table("Users", Schema = "dbo")]
    private class EntityWithIdentity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }

    [Table("Users", Schema = "dbo")]
    private class TwoColumnEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Email { get; set; }
    }

    [Table("LargeTable", Schema = "dbo")]
    private class ManyColumnsEntity
    {
        [Key]
        public string Col1 { get; set; } = string.Empty;
        public string Col2 { get; set; } = string.Empty;
        public string Col3 { get; set; } = string.Empty;
        public string Col4 { get; set; } = string.Empty;
        public string Col5 { get; set; } = string.Empty;
        public string Col6 { get; set; } = string.Empty;
        public string Col7 { get; set; } = string.Empty;
        public string Col8 { get; set; } = string.Empty;
        public string Col9 { get; set; } = string.Empty;
        public string Col10 { get; set; } = string.Empty;
        public string Col11 { get; set; } = string.Empty;
        public string Col12 { get; set; } = string.Empty;
        public string Col13 { get; set; } = string.Empty;
        public string Col14 { get; set; } = string.Empty;
        public string Col15 { get; set; } = string.Empty;
        public string Col16 { get; set; } = string.Empty;
        public string Col17 { get; set; } = string.Empty;
        public string Col18 { get; set; } = string.Empty;
        public string Col19 { get; set; } = string.Empty;
        public string Col20 { get; set; } = string.Empty;
    }

    #endregion
}
