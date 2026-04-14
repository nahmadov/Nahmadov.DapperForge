using ConnectionSample;
using ConnectionSample.Sqlite;

using Nahmadov.DapperForge.Core.Infrastructure.Extensions;
using Nahmadov.DapperForge.Oracle.Extensions;
using Nahmadov.DapperForge.SqlServer.Extensions;
using Nahmadov.DapperForge.Sqlite.Extensions;
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
        // ── SQL Server / Oracle context ────────────────────────────────────────
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

        // ── SQLite context (file-based, no credentials required) ──────────────
        var dbPath = Path.Combine(Path.GetTempPath(), "dapperforge_sample.db");
        services.AddDapperDbContext<SqliteDbContext>(options =>
        {
            options.UseSqlite($"Data Source={dbPath}");
            options.EnableSqlLogging(); // optional, logs generated SQL to console for demonstration purposes
        });

        services.AddTransient<SqliteSampleRunner>();
    })
    .Build();

// ── Run SQLite sample (always available, no external DB needed) ───────────────
using (var sqliteScope = host.Services.CreateScope())
{
    var sqliteRunner = sqliteScope.ServiceProvider.GetRequiredService<SqliteSampleRunner>();
    await sqliteRunner.RunAsync();
}

// ── Run SQL Server / Oracle sample (requires connection string) ───────────────
// try
// {
//     using var scope = host.Services.CreateScope();
//     var runner = scope.ServiceProvider.GetRequiredService<SampleRunner>();
//     await runner.RunAsync();
// }
// catch (InvalidOperationException ex) when (ex.Message.Contains("ConnectionString") || ex.Message.Contains("missing"))
// {
//     Console.WriteLine($"\n[SQL Server/Oracle sample skipped] {ex.Message}");
//     Console.WriteLine("Set FullSample:SqlServerConnectionString in user secrets to run the full sample.");
// }

