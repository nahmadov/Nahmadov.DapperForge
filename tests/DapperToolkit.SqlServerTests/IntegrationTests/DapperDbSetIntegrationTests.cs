using Microsoft.Extensions.Configuration;

using DapperToolkit.Core.Interfaces;
using DapperToolkit.SqlServer;
using DapperToolkit.SqlServer.Context;

namespace DapperToolkit.SqlServerTests.IntegrationTests;

public class DapperDbSetIntegrationTests
{
    private readonly DapperDbContext _dbContext;
    private readonly IDapperDbSet<TestEntity> _dbSet;

    public DapperDbSetIntegrationTests()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("testsettings.json")
            .Build();

        var connectionString = configuration.GetConnectionString("SqlServer")!;
        _dbContext = new DapperDbContext(new SqlServerConnectionProvider(connectionString));
        _dbSet = _dbContext.Set<TestEntity>();
    }

    [Fact]
    public async Task ToListAsync_Should_Return_Inserted_Entities()
    {
        // Arrange
        var name1 = string.Concat("ListItem_", Guid.NewGuid().ToString("N").AsSpan(0, 5));
        var name2 = string.Concat("ListItem_", Guid.NewGuid().ToString("N").AsSpan(0, 5));

        await _dbContext.ExecuteAsync("INSERT INTO TestEntities (NameNew) VALUES (@Name)", new { Name = name1 });
        await _dbContext.ExecuteAsync("INSERT INTO TestEntities (NameNew) VALUES (@Name)", new { Name = name2 });

        // Act
        var list = await _dbSet.ToListAsync();

        // Assert
        Assert.Contains(list, x => x.Name == name1);
        Assert.Contains(list, x => x.Name == name2);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_Should_Return_Matching_Entity()
    {
        // Arrange
        var uniqueName = string.Concat("FirstItem_", Guid.NewGuid().ToString("N").AsSpan(0, 6));
        await _dbContext.ExecuteAsync("INSERT INTO TestEntities (NameNew) VALUES (@Name)", new { Name = uniqueName });

        // Act
        var entity = await _dbSet.FirstOrDefaultAsync(x => x.Name == uniqueName);

        // Assert
        Assert.NotNull(entity);
        Assert.Equal(uniqueName, entity!.Name);
    }
}