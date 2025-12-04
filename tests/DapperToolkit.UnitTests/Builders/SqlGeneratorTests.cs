using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using DapperToolkit.Core.Builders;
using DapperToolkit.Core.Mapping;
using DapperToolkit.Oracle;
using DapperToolkit.SqlServer;
using Xunit;

namespace DapperToolkit.UnitTests.Builders;

public class SqlGeneratorTests
{
    [Fact]
    public void SelectAll_IncludesSchemaAndAliases()
    {
        var generator = new SqlGenerator<UserEntity>(SqlServerDialect.Instance, BuildMapping());

        var sql = generator.SelectAllSql;

        Assert.Contains("FROM [dbo].[Users]", sql);
        Assert.Contains("[username] AS [Name]", sql);
        Assert.Contains("[IsActive] AS [IsActive]", sql);
        Assert.Contains("[Id] AS [Id]", sql);
    }

    [Fact]
    public void Insert_SkipsIdentityAndUsesParameters()
    {
        var generator = new SqlGenerator<UserEntity>(SqlServerDialect.Instance, BuildMapping());

        var sql = generator.InsertSql;

        Assert.DoesNotContain("[Id]", sql);
        Assert.Contains("[username]", sql);
        Assert.Contains("@Name", sql);
        Assert.Contains("[IsActive]", sql);
        Assert.Contains("@IsActive", sql);
    }

    [Fact]
    public void Update_SetsNonKeyColumns_And_FiltersByKey()
    {
        var generator = new SqlGenerator<UserEntity>(SqlServerDialect.Instance, BuildMapping());

        var sql = generator.UpdateSql;

        Assert.Contains("UPDATE [dbo].[Users] SET", sql);
        Assert.Contains("[username] = @Name", sql);
        Assert.Contains("[IsActive] = @IsActive", sql);
        Assert.EndsWith("WHERE [Id] = @Id", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Delete_FiltersByKey()
    {
        var generator = new SqlGenerator<UserEntity>(SqlServerDialect.Instance, BuildMapping());

        var sql = generator.DeleteByIdSql;

        Assert.Equal("DELETE FROM [dbo].[Users] WHERE [Id] = @Id", sql);
    }

    [Fact]
    public void InsertReturningId_IsGeneratedForSqlServer()
    {
        var generator = new SqlGenerator<UserEntity>(SqlServerDialect.Instance, BuildMapping());

        Assert.NotNull(generator.InsertReturningIdSql);
        Assert.Contains("SCOPE_IDENTITY()", generator.InsertReturningIdSql);
    }

    [Fact]
    public void InsertReturningId_IsNullForOracle()
    {
        var mapping = BuildMapping();
        var generator = new SqlGenerator<UserEntity>(OracleDialect.Instance, mapping);

        Assert.Null(generator.InsertReturningIdSql);
    }

    [Table("Users", Schema = "dbo")]
    private class UserEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("username")]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }

    private static EntityMapping BuildMapping()
    {
        var builder = new DapperModelBuilder(SqlServerDialect.Instance);
        builder.Entity<UserEntity>();
        return builder.Build()[typeof(UserEntity)];
    }
}
