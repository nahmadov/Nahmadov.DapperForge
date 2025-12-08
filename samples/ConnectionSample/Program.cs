using ConnectionSample;

using DapperToolkit.Core.Extensions;
using DapperToolkit.Oracle.Extensions;
using DapperToolkit.SqlServer.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddUserSecrets<Program>();
    })
    .ConfigureServices((ctx, services) =>
    {
        // Expect user secrets:
        // FullSample:Provider = SqlServer (default) or Oracle
        // FullSample:SqlServerConnectionString or FullSample:OracleConnectionString
        var sampleSection = ctx.Configuration.GetSection("FullSample");
        var provider = sampleSection["Provider"] ?? "SqlServer";

        services.AddDapperDbContext<AppDapperDbContext>(options =>
        {
            if (string.Equals(provider, "Oracle", StringComparison.OrdinalIgnoreCase))
            {
                var oracleConn = sampleSection["OracleConnectionString"];
                options.UseOracle(oracleConn ?? throw new InvalidOperationException("FullSample:OracleConnectionString is missing."));
            }
            else
            {
                var sqlConn = sampleSection["SqlServerConnectionString"] ?? sampleSection["ConnectionString"];
                options.UseSqlServer(sqlConn ?? throw new InvalidOperationException("FullSample:SqlServerConnectionString is missing."));
            }
        });

        services.AddTransient<SampleRunner>();
    })
    .Build();

using var scope = host.Services.CreateScope();
var runner = scope.ServiceProvider.GetRequiredService<SampleRunner>();

await runner.RunAsync();
