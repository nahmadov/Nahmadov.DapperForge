using Microsoft.Extensions.Configuration;

namespace DapperToolkit.SqlServerTests.IntegrationTests;

public static class TestHelper
{
    public static string GetTestConnectionString()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("testsettings.json")
            .Build();

        return config.GetConnectionString("SqlServer")!;
    }
}