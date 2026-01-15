using System.Collections.Generic;
using System.Linq;

using Nahmadov.DapperForge.Core.Builders;
using Nahmadov.DapperForge.Core.Common;
using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.SqlServer;
using Nahmadov.DapperForge.UnitTests.Fakes;
using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Context;

public class HistoryTypeCrudIncludeTests
{
    private sealed class HistoryType
    {
        public int HistoryTypeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<History> Histories { get; set; } = new();
    }

    private sealed class History
    {
        public int Id { get; set; }
        public int HistoryTypeId { get; set; }
        public HistoryType? HistoryType { get; set; }
        public string? Notes { get; set; }
    }

    private sealed class HistoryContext : DapperDbContext
    {
        public DapperSet<HistoryType> HistoryTypes => Set<HistoryType>();
        public DapperSet<History> Histories => Set<History>();

        public HistoryContext(DapperDbContextOptions options) : base(options) { }

        protected override void OnModelCreating(DapperModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HistoryType>(b =>
            {
                b.ToTable("HistType");
                b.Property(x => x.HistoryTypeId).HasColumnName("HistType");
                b.HasKey(x => x.HistoryTypeId);
            });

            modelBuilder.Entity<History>(b =>
            {
                b.ToTable("History");
                b.HasKey(x => x.Id);
                b.HasOne<HistoryType>(h => h.HistoryType)
                    .WithMany(ht => ht.Histories)
                    .HasForeignKey(h => h.HistoryTypeId);
            });
        }
    }

    private static (HistoryContext ctx, FakeDbConnection conn) CreateContext()
    {
        var conn = new FakeDbConnection();
        var options = new DapperDbContextOptions
        {
            ConnectionFactory = () => conn,
            Dialect = SqlServerDialect.Instance
        };

        return (new HistoryContext(options), conn);
    }

    [Fact]
    public async Task InsertAsync_HistoryType_Executes()
    {
        var (ctx, conn) = CreateContext();
        conn.SetupExecute(1);

        var historyType = new HistoryType { HistoryTypeId = 1, Name = "Audit" };

        var affected = await ctx.HistoryTypes.InsertAsync(historyType);

        Assert.Equal(1, affected);
    }

    [Fact]
    public async Task UpdateAsync_HistoryType_Executes()
    {
        var (ctx, conn) = CreateContext();
        conn.SetupExecute(1);

        var historyType = new HistoryType { HistoryTypeId = 1, Name = "Audit Updated" };

        var affected = await ctx.HistoryTypes.UpdateAsync(historyType);

        Assert.Equal(1, affected);
    }

    [Fact]
    public async Task DeleteAsync_HistoryType_Executes()
    {
        var (ctx, conn) = CreateContext();
        conn.SetupExecute(1);

        var historyType = new HistoryType { HistoryTypeId = 1 };

        var affected = await ctx.HistoryTypes.DeleteAsync(historyType);

        Assert.Equal(1, affected);
    }

    [Fact]
    public async Task GetAllAsync_HistoryTypes_ReturnsResults()
    {
        var (ctx, conn) = CreateContext();

        conn.SetupQuery(new[]
        {
            new HistoryType { HistoryTypeId = 1, Name = "Audit" },
            new HistoryType { HistoryTypeId = 2, Name = "Change" }
        });

        var results = (await ctx.HistoryTypes.GetAllAsync()).ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.HistoryTypeId == 1);
        Assert.Contains(results, r => r.HistoryTypeId == 2);
    }

    [Fact]
    public async Task Query_History_WithInclude_LoadsHistoryType()
    {
        var (ctx, conn) = CreateContext();

        var history = new History { Id = 10, HistoryTypeId = 7, Notes = "Created" };
        var historyType = new HistoryType { HistoryTypeId = 7, Name = "Audit" };

        conn.SetupQuery(new[] { history });
        conn.SetupSplitQuery(new[] { historyType });

        var results = (await ctx.Histories.Query()
            .Include(h => h.HistoryType)
            .AsSplitQuery()
            .ToListAsync()).ToList();

        var result = Assert.Single(results);
        Assert.NotNull(result.HistoryType);
        Assert.Equal("Audit", result.HistoryType!.Name);
    }
}
