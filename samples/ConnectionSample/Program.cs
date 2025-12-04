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

// Insert a demo user
await report.AddUserAsync("alice", isActive: true);

var deleted = await report.DeleteOldLogsAsync();
Console.WriteLine($"Deleted rows: {deleted}");

// Fetch active users with case-insensitive starts-with filter
var users = await report.GetActiveUsersAsync(startsWith: "a");
foreach (var u in users)
{
    Console.WriteLine($"{u.Id} - {u.Name} (active: {u.IsActive})");
}
