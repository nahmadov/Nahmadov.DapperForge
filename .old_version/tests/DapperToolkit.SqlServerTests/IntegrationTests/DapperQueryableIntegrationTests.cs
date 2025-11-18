using Microsoft.Extensions.Configuration;

using DapperToolkit.SqlServer;
using DapperToolkit.SqlServer.Context;
using DapperToolkit.SqlServer.Query;

namespace DapperToolkit.SqlServerTests.IntegrationTests;

public class DapperQueryableIntegrationTests
{
    private readonly DapperDbContext _dbContext;

    public DapperQueryableIntegrationTests()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("testsettings.json")
            .Build();

        var connStr = config.GetConnectionString("SqlServer")!;
        _dbContext = new DapperDbContext(new SqlServerConnectionProvider(connStr));
    }

    [Fact]
    public async Task ToListAsync_Should_Filter_By_Where()
    {
        // Arrange
        string name = "ActiveUser_" + Guid.NewGuid().ToString("N")[..6];
        string insertSql = "INSERT INTO TestEntities (NameNew, IsActive) VALUES (@Name, @IsActive)";
        await _dbContext.ExecuteAsync(insertSql, new { Name = name, IsActive = true });
        await _dbContext.ExecuteAsync(insertSql, new { Name = "Inactive_" + name, IsActive = false });

        // Act
        var queryable = new DapperQueryable<TestEntity>(_dbContext)
                            .Where(x => x.Name == name && x.IsActive);

        var result = await queryable.ToListAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(name, result[0].Name);
        Assert.True(result[0].IsActive);
    }
}