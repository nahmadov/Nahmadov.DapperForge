using Microsoft.Extensions.DependencyInjection;

using DapperToolkit.Core.Interfaces;
using DapperToolkit.SqlServer;
using DapperToolkit.SqlServer.Context;
using DapperToolkit.SqlServer.Extensions;

namespace DapperToolkit.SqlServerTests;

public class DapperSqlServiceCollectionExtensionsTests
{
    private const string ConnectionString = "Server=.;Database=TestDb;Trusted_Connection=True;";

    [Fact]
    public void Should_Register_SqlServer_DapperDbContext()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddDapperDbContextWithSql<DapperDbContext>(ConnectionString);

        var provider = services.BuildServiceProvider();

        // Act
        var dbContext = provider.GetService<IDapperDbContext>();
        var providerInstance = provider.GetService<IDapperConnectionProvider>();

        // Assert
        Assert.NotNull(dbContext);
        Assert.IsType<DapperDbContext>(dbContext);

        Assert.NotNull(providerInstance);
        Assert.IsType<SqlServerConnectionProvider>(providerInstance);
    }
}