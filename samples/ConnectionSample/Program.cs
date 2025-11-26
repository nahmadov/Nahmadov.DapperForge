using ConnectionSample;

using DapperToolkit.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using DapperToolkit.SqlServer.Extensions;
using Microsoft.Extensions.Configuration;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
            // Əsas hissə budur 🔽
            config.AddUserSecrets<Program>();
    })
    .ConfigureServices((ctx, services) =>
    {
        var connStr = ctx.Configuration.GetSection("ConnectionSample")["ConnectionString"];
        services.AddDapperDbContext<AppDapperDbContext>(options =>
        {
            options.UseSqlServer(connStr ?? throw new InvalidOperationException("Connection string not found."));
        });

        services.AddTransient<ReportService>();
    })
    .Build();

var report = host.Services.GetRequiredService<ReportService>();

var deleted = await report.DeleteOldLogsAsync();
Console.WriteLine($"Deleted rows: {deleted}");

var users = await report.GetUsersAsync();
foreach (var u in users)
{
    Console.WriteLine($"{u.Id} - {u.Name}");
}