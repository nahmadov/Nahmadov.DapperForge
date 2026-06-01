using System.ComponentModel.DataAnnotations;

using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Core.Modeling.Builders;
using Nahmadov.DapperForge.Core.Modeling.Mapping;
using Nahmadov.DapperForge.Core.Modeling.Schema;
using Nahmadov.DapperForge.Oracle;
using Nahmadov.DapperForge.SqlServer;

using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Schema;

public class HasColumnTypeMappingTests
{
    private static EntityMapping BuildMapping(Action<EntityTypeBuilder<Sample>> configure)
    {
        var modelBuilder = new DapperModelBuilder(SqlServerDialect.Instance);
        modelBuilder.Entity(configure);
        return modelBuilder.Build()[typeof(Sample)];
    }

    private static PropertyMapping Column(EntityMapping mapping, string propertyName)
        => mapping.PropertyMappings.Single(p => p.Property.Name == propertyName);

    [Fact]
    public void ResolveColumnType_NoConfig_FallsBackToInference()
    {
        var mapping = BuildMapping(b => b.HasKey(s => s.Id));

        var (type, _) = Column(mapping, nameof(Sample.Amount)).ResolveColumnType();

        Assert.Equal(SqlColumnType.Decimal, type); // inferred from decimal
    }

    [Fact]
    public void HasColumnType_OverridesInference()
    {
        var mapping = BuildMapping(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.Amount).HasColumnType(SqlColumnType.Money);
        });

        var (type, _) = Column(mapping, nameof(Sample.Amount)).ResolveColumnType();

        Assert.Equal(SqlColumnType.Money, type);
    }

    [Fact]
    public void HasColumnType_WithPrecisionScale_ResolvesToSql()
    {
        var mapping = BuildMapping(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.Amount).HasColumnType(SqlColumnType.Decimal, 18, 4);
        });

        var (type, facets) = Column(mapping, nameof(Sample.Amount)).ResolveColumnType();
        var sql = SqlServerDialect.Instance.GetColumnTypeSql(type, facets);

        Assert.Equal("decimal(18,4)", sql);
    }

    [Fact]
    public void HasMaxLength_FeedsNVarCharLength_WhenNoExplicitColumnType()
    {
        var mapping = BuildMapping(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.Name).HasMaxLength(50);
        });

        var (type, facets) = Column(mapping, nameof(Sample.Name)).ResolveColumnType();
        var sql = SqlServerDialect.Instance.GetColumnTypeSql(type, facets);

        Assert.Equal(SqlColumnType.NVarChar, type);
        Assert.Equal("nvarchar(50)", sql);
    }

    [Fact]
    public void HasColumnType_WithLength_OverridesMaxLength()
    {
        var mapping = BuildMapping(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.Name).HasMaxLength(50).HasColumnType(SqlColumnType.VarChar, 200);
        });

        var (type, facets) = Column(mapping, nameof(Sample.Name)).ResolveColumnType();
        var sql = SqlServerDialect.Instance.GetColumnTypeSql(type, facets);

        Assert.Equal("varchar(200)", sql);
    }

    [Fact]
    public void OracleDialect_GetColumnTypeSql_Throws()
    {
        Assert.Throws<NotSupportedException>(
            () => ((ISqlDialect)OracleDialect.Instance).GetColumnTypeSql(SqlColumnType.Int, default));
    }

    private sealed class Sample
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
