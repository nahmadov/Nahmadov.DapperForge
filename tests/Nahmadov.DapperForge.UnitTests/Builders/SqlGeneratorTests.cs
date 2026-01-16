using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Nahmadov.DapperForge.Core.Builders;
using Nahmadov.DapperForge.Core.Mapping;
using Nahmadov.DapperForge.Oracle;
using Nahmadov.DapperForge.SqlServer;
using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Builders;

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

        Assert.Contains("WHERE a.[TenantId] = @TenantId AND a.[UserId] = @UserId", generator.SelectByIdSql);
        Assert.Contains("WHERE [TenantId] = @TenantId AND [UserId] = @UserId", generator.DeleteByIdSql);
    }

    [Fact]
    public void SelectAll_Includes_Enum_Columns_With_Alias()
    {
        var mapping = BuildEnumMapping();
        var generator = new SqlGenerator<EnumEntity>(SqlServerDialect.Instance, mapping);

        var sql = generator.SelectAllSql;

        Assert.Contains("a.[Status] AS [Status]", sql);
        Assert.Contains("a.[NullableStatus] AS [NullableStatus]", sql);
    }

    [Fact]
    public void InsertAndUpdate_Handle_Enum_Columns()
    {
        var mapping = BuildEnumMapping();
        var generator = new SqlGenerator<EnumEntity>(SqlServerDialect.Instance, mapping);

        Assert.Contains("[Status]", generator.InsertSql);
        Assert.Contains("@Status", generator.InsertSql);
        Assert.Contains("[NullableStatus]", generator.InsertSql);

        Assert.Contains("[Status] = @Status", generator.UpdateSql);
        Assert.Contains("[NullableStatus] = @NullableStatus", generator.UpdateSql);
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

    [Fact]
    public void CompositeAlternateKey_Uses_All_Key_Columns_In_Where()
    {
        var mapping = BuildCompositeAlternateKeyMapping();
        var generator = new SqlGenerator<CompositeAlternateKeyEntity>(SqlServerDialect.Instance, mapping);

        Assert.Contains("WHERE a.[TenantId] = @TenantId AND a.[Code] = @Code", generator.SelectByIdSql);
        Assert.Contains("WHERE [TenantId] = @TenantId AND [Code] = @Code", generator.DeleteByIdSql);
        Assert.Contains("WHERE [TenantId] = @TenantId AND [Code] = @Code", generator.UpdateSql);
    }

    [Fact]
    public void CompositeAlternateKey_WithAnonymousType_Uses_All_Key_Columns_In_Where()
    {
        var mapping = BuildCompositeAlternateKeyAnonymousMapping();
        var generator = new SqlGenerator<CompositeAlternateKeyEntity>(SqlServerDialect.Instance, mapping);

        Assert.Contains("WHERE a.[TenantId] = @TenantId AND a.[Code] = @Code", generator.SelectByIdSql);
        Assert.Contains("WHERE [TenantId] = @TenantId AND [Code] = @Code", generator.DeleteByIdSql);
        Assert.Contains("WHERE [TenantId] = @TenantId AND [Code] = @Code", generator.UpdateSql);
    }

    [Fact]
    public void CompositeKey_WithAnonymousType_Uses_All_Key_Columns_In_Where()
    {
        var builder = new DapperModelBuilder(SqlServerDialect.Instance);
        builder.Entity<CompositeEntity>(b =>
        {
            b.ToTable("Users", "dbo");
            b.HasKey(e => new { e.TenantId, e.UserId });
        });
        var mapping = builder.Build()[typeof(CompositeEntity)];
        var generator = new SqlGenerator<CompositeEntity>(SqlServerDialect.Instance, mapping);

        Assert.Contains("WHERE a.[TenantId] = @TenantId AND a.[UserId] = @UserId", generator.SelectByIdSql);
        Assert.Contains("WHERE [TenantId] = @TenantId AND [UserId] = @UserId", generator.DeleteByIdSql);
    }

    [Fact]
    public void CompositeAlternateKey_ExcludesKeyColumnsFromUpdate()
    {
        var mapping = BuildCompositeAlternateKeyMapping();
        var generator = new SqlGenerator<CompositeAlternateKeyEntity>(SqlServerDialect.Instance, mapping);

        // Key columns should not appear in SET clause
        Assert.DoesNotContain("[TenantId] =", generator.UpdateSql.Split("WHERE")[0]);
        Assert.DoesNotContain("[Code] =", generator.UpdateSql.Split("WHERE")[0]);
        // But Name should be updatable
        Assert.Contains("[Name] = @Name", generator.UpdateSql);
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

    private static EntityMapping BuildEnumMapping()
    {
        var builder = new DapperModelBuilder(SqlServerDialect.Instance);
        builder.Entity<EnumEntity>(b =>
        {
            b.ToTable("EnumEntities", "dbo");
            b.Property(e => e.Id).AutoGenerated(false);
        });
        return builder.Build()[typeof(EnumEntity)];
    }

    private class CompositeEntity
    {
        public int TenantId { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class CompositeAlternateKeyEntity
    {
        public int TenantId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private static EntityMapping BuildCompositeAlternateKeyMapping()
    {
        var builder = new DapperModelBuilder(SqlServerDialect.Instance);
        builder.Entity<CompositeAlternateKeyEntity>(b =>
        {
            b.ToTable("Products", "dbo");
            b.HasNoKey();
            b.HasAlternateKey(e => e.TenantId, e => e.Code);
        });
        return builder.Build()[typeof(CompositeAlternateKeyEntity)];
    }

    private static EntityMapping BuildCompositeAlternateKeyAnonymousMapping()
    {
        var builder = new DapperModelBuilder(SqlServerDialect.Instance);
        builder.Entity<CompositeAlternateKeyEntity>(b =>
        {
            b.ToTable("Products", "dbo");
            b.HasNoKey();
            b.HasAlternateKey(e => new { e.TenantId, e.Code });
        });
        return builder.Build()[typeof(CompositeAlternateKeyEntity)];
    }

    private class OracleSeqEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private enum OrderStatus
    {
        Pending = 0,
        Shipped = 1,
        Delivered = 2
    }

    private class EnumEntity
    {
        [Key]
        public int Id { get; set; }
        public OrderStatus Status { get; set; }
        public OrderStatus? NullableStatus { get; set; }
    }
}
