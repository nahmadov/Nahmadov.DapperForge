using Microsoft.Extensions.DependencyInjection;

using DapperToolkit.Core.Interfaces;
using DapperToolkit.Oracle;
using DapperToolkit.Oracle.Context;
using DapperToolkit.Oracle.Extensions;

namespace DapperToolkit.OracleTests;

public class DapperSqlServiceCollectionExtensionsTests
{
    private const string ConnectionString = "Server=.;Database=TestDb;Trusted_Connection=True;";

    [Fact]
    public void Should_Register_Oracle_DapperDbContext()
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
        Assert.IsType<OracleConnectionProvider>(providerInstance);
    }
}