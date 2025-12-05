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
    public void InsertReturningId_IsGeneratedForOracle()
    {
        var mapping = BuildMapping();
        var generator = new SqlGenerator<UserEntity>(OracleDialect.Instance, mapping);

        Assert.NotNull(generator.InsertReturningIdSql);
        Assert.Contains("RETURNING", generator.InsertReturningIdSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompositeKey_Uses_All_Key_Columns_In_Where()
    {
        var mapping = BuildCompositeMapping();
        var generator = new SqlGenerator<CompositeEntity>(SqlServerDialect.Instance, mapping);

        Assert.Contains("WHERE [TenantId] = @TenantId AND [UserId] = @UserId", generator.SelectByIdSql);
        Assert.Contains("WHERE [TenantId] = @TenantId AND [UserId] = @UserId", generator.DeleteByIdSql);
    }

    [Fact]
    public void OracleSequence_Uses_NextVal_In_Insert()
    {
        var builder = new DapperModelBuilder(OracleDialect.Instance);
        builder.Entity<OracleSeqEntity>(b =>
        {
            b.ToTable("Users");
            b.HasKey(e => e.Id);
            b.Property(e => e.Id).HasSequence("user_seq");
            b.Property(e => e.Name).IsRequired();
        });
        var mapping = builder.Build()[typeof(OracleSeqEntity)];

        var generator = new SqlGenerator<OracleSeqEntity>(OracleDialect.Instance, mapping);

        Assert.Contains("\"user_seq\".NEXTVAL", generator.InsertSql);
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

    private static EntityMapping BuildCompositeMapping()
    {
        var builder = new DapperModelBuilder(SqlServerDialect.Instance);
        builder.Entity<CompositeEntity>(b =>
        {
            b.ToTable("Users", "dbo");
            b.HasKey(e => e.TenantId, e => e.UserId);
        });
        return builder.Build()[typeof(CompositeEntity)];
    }

    private class CompositeEntity
    {
        public int TenantId { get; set; }
        public int UserId { get; set; }
    }

    private class OracleSeqEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
