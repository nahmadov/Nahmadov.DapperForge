using System.Data;

using Dapper;

using Nahmadov.DapperForge.Core.Modeling.Schema;
using Nahmadov.DapperForge.Sqlite.Date;

namespace ConnectionSample.Sqlite;

/// <summary>
/// Demonstrates SQLite-specific date/time predicate support:
///   • SqliteDate.Date(e.OccursAt)     — filters on date part of a DateTime column
///   • SqliteDate.DateTime(e.OccursAt) — normalised datetime comparison
///   • e.Date.CompareTo(...)           — ordered comparison on a string date column
///   • string.Compare(e.Date, ...)     — static ordered comparison on a string column
/// </summary>
public class SqliteSampleRunner(SqliteDbContext db)
{
    private readonly SqliteDbContext _db = db;

    public async Task RunAsync()
    {
        Print("=== SQLite Date/Time Sample ===");

        await _db.EnsureSchemaAsync();
        await SeedAsync();

        await RunDateTimeColumnSamplesAsync();
        await RunStringColumnSamplesAsync();
        await RunTempTableSampleAsync();
        await RunDataTableTempTableSampleAsync();

        Print("=== SQLite sample complete ===\n");
    }

    // ── Temp table builder (Phase 2) ──────────────────────────────────────────

    private async Task RunTempTableSampleAsync()
    {
        Print("── Session temp table (fluent builder) ───────────────────────────");

        // Temp tables are connection-scoped, so create and use them on the SAME connection.
        using var scope = _db.CreateConnectionScope();

        var builder = _db.TempTable("TempHistDaily")
            .Column<int>("MVSID")
            .Column<int>("HistColID")
            .Column<DateTime>("HistDate")
            .Column("Value", SqlColumnType.Decimal, new ColumnTypeFacets(Precision: 18, Scale: 4))
            .Column<DateTime>("InsertDate", nullable: true);

        Print($"  DDL: {builder.BuildCreateTableSql()}");

        await builder.CreateAsync(scope.Connection);

        // Write to and read back from the temp table on the same connection.
        await scope.Connection.ExecuteAsync(
            @"INSERT INTO ""TempHistDaily"" (""MVSID"",""HistColID"",""HistDate"",""Value"",""InsertDate"")
              VALUES (@MVSID,@HistColID,@HistDate,@Value,@InsertDate)",
            new { MVSID = 1, HistColID = 10, HistDate = DateTime.Today, Value = 42.5m, InsertDate = (DateTime?)null });

        var count = await scope.Connection.ExecuteScalarAsync<long>(
            @"SELECT COUNT(*) FROM ""TempHistDaily""");

        Print($"  Created temp table and inserted rows: {count}");
        Print("");
    }

    // ── Temp table from a DataTable (Phase 3) ─────────────────────────────────

    private async Task RunDataTableTempTableSampleAsync()
    {
        Print("── Session temp table (from a DataTable) ─────────────────────────");

        // A DataTable is the natural source when the caller already has one for bulk copy.
        var schema = new DataTable("Import");
        schema.Columns.Add(new DataColumn("MVSID", typeof(int)) { AllowDBNull = false });
        schema.Columns.Add(new DataColumn("Value", typeof(double)) { AllowDBNull = false });
        schema.Columns.Add(new DataColumn("ImportedAt", typeof(DateTime)) { AllowDBNull = false });
        schema.Columns.Add(new DataColumn("Note", typeof(string)) { MaxLength = 50, AllowDBNull = true });

        using var scope = _db.CreateConnectionScope();

        await _db.CreateTempTableFromAsync("TempImport", schema, scope.Connection);

        var exists = await scope.Connection.ExecuteScalarAsync<long>(
            @"SELECT COUNT(*) FROM sqlite_temp_master WHERE type='table' AND name='TempImport'");

        Print($"  Derived temp table from DataTable ({schema.Columns.Count} columns); exists in session: {exists == 1}");
        Print("");
    }

    // ── Seed ─────────────────────────────────────────────────────────────────

    private async Task SeedAsync()
    {
        var existingEvents = await _db.Events.CountAsync(_ => true);
        if (existingEvents > 0)
        {
            Print("  [seed] tables already populated, skipping.\n");
            return;
        }

        Print("  [seed] inserting test data...");

        // CalendarEvents — DateTime column, various dates/times
        var events = new[]
        {
            new CalendarEvent { Title = "Morning standup",   OccursAt = Today(09, 30), Category = "Work"    },
            new CalendarEvent { Title = "Lunch with team",   OccursAt = Today(12, 00), Category = "Social"  },
            new CalendarEvent { Title = "Code review",       OccursAt = Today(15, 00), Category = "Work"    },
            new CalendarEvent { Title = "Yesterday meeting", OccursAt = Yesterday(10, 00), Category = "Work" },
            new CalendarEvent { Title = "Last week event",   OccursAt = DaysAgo(7, 09, 00), Category = "Work" },
            new CalendarEvent { Title = "Last month event",  OccursAt = DaysAgo(35, 11, 00), Category = "Work" },
            new CalendarEvent { Title = "Next week event",   OccursAt = DaysAhead(7, 14, 00), Category = "Work" },
        };

        foreach (var ev in events)
            await _db.Events.InsertAsync(ev);

        // Appointments — separate string Date + Time columns
        var appointments = new[]
        {
            new Appointment { Title = "Dentist",          Date = "2024-03-15", Time = "10:00:00", Status = "Done"    },
            new Appointment { Title = "Tax filing",       Date = "2024-04-30", Time = "09:00:00", Status = "Done"    },
            new Appointment { Title = "Annual review",    Date = "2024-06-01", Time = "14:00:00", Status = "Done"    },
            new Appointment { Title = "Conference",       Date = "2024-09-20", Time = "08:00:00", Status = "Done"    },
            new Appointment { Title = "Year-end report",  Date = "2024-12-20", Time = "16:00:00", Status = "Done"    },
            new Appointment { Title = "Q1 planning",      Date = "2025-01-10", Time = "10:00:00", Status = "Pending" },
            new Appointment { Title = "Team offsite",     Date = "2025-03-05", Time = "09:00:00", Status = "Pending" },
            new Appointment { Title = "Budget meeting",   Date = "2025-06-15", Time = "13:00:00", Status = "Pending" },
        };

        foreach (var ap in appointments)
            await _db.Appointments.InsertAsync(ap);

        Print("  [seed] done.\n");
    }

    // ── DateTime column samples ───────────────────────────────────────────────

    private async Task RunDateTimeColumnSamplesAsync()
    {
        Print("── DateTime column (SqliteDate markers) ──────────────────────────");

        // 1. Filter by exact date — time part is ignored
        var todayEvents = await _db.Events.WhereAsync(
            e => SqliteDate.Date(e.OccursAt) == DateTime.Today);

        Print($"  [1] Events on today ({DateTime.Today:yyyy-MM-dd}): {todayEvents.Count()} found");
        foreach (var ev in todayEvents)
            Print($"      • {ev.OccursAt:yyyy-MM-dd HH:mm} — {ev.Title}");

        // 2. Filter within the last 7 days (date part only)
        var weekAgo = DateTime.Today.AddDays(-7);
        var recentEvents = await _db.Events.WhereAsync(
            e => SqliteDate.Date(e.OccursAt) >= DateTime.Today.AddDays(-7) &&
                 SqliteDate.Date(e.OccursAt) <= DateTime.Today);

        Print($"\n  [2] Events in last 7 days: {recentEvents.Count()} found");
        foreach (var ev in recentEvents)
            Print($"      • {ev.OccursAt:yyyy-MM-dd HH:mm} — {ev.Title}");

        // 3. Datetime comparison — full precision (today after 11:00)
        var cutoff = DateTime.Today.AddHours(11);
        var afternoonEvents = await _db.Events.WhereAsync(
            e => SqliteDate.Date(e.OccursAt) == DateTime.Today &&
                 SqliteDate.DateTime(e.OccursAt) > cutoff);

        Print($"\n  [3] Today's events after {cutoff:HH:mm}: {afternoonEvents.Count()} found");
        foreach (var ev in afternoonEvents)
            Print($"      • {ev.OccursAt:yyyy-MM-dd HH:mm} — {ev.Title}");

        // 4. Future events only
        var futureEvents = await _db.Events.WhereAsync(
            e => SqliteDate.Date(e.OccursAt) > DateTime.Today);

        Print($"\n  [4] Future events (date > today): {futureEvents.Count()} found");
        foreach (var ev in futureEvents)
            Print($"      • {ev.OccursAt:yyyy-MM-dd HH:mm} — {ev.Title}");

        // 5. Nullable column scenario — using QueryAsync with raw SqliteDate expression
        //    (demonstrating that date() on null returns NULL, no false positives)
        Print($"\n  [5] Nullable date demo: date(NULL) in SQLite returns NULL — rows excluded automatically.");

        Print("");
    }

    // ── String column samples ─────────────────────────────────────────────────

    private async Task RunStringColumnSamplesAsync()
    {
        Print("── String Date column (CompareTo / string.Compare) ───────────────");

        // 1. Exact date match — plain == works for string columns
        var exactDate = await _db.Appointments.WhereAsync(
            a => a.Date == "2024-03-15");

        Print($"  [1] Appointments on 2024-03-15 (exact ==): {exactDate.Count()} found");
        foreach (var ap in exactDate)
            Print($"      • {ap.Date} {ap.Time} — {ap.Title}");

        // 2. Range query using CompareTo — C# doesn't allow > on strings directly
        var inRange = await _db.Appointments.WhereAsync(
            a => a.Date.CompareTo("2024-06-01") >= 0 &&
                 a.Date.CompareTo("2024-12-31") <= 0);

        Print($"\n  [2] Appointments between 2024-06-01 and 2024-12-31 (CompareTo): {inRange.Count()} found");
        foreach (var ap in inRange)
            Print($"      • {ap.Date} {ap.Time} — {ap.Title}  [{ap.Status}]");

        // 3. Same range using static string.Compare
        var inRange2 = await _db.Appointments.WhereAsync(
            a => string.Compare(a.Date, "2025-01-01") >= 0);

        Print($"\n  [3] Appointments from 2025 onwards (string.Compare): {inRange2.Count()} found");
        foreach (var ap in inRange2)
            Print($"      • {ap.Date} {ap.Time} — {ap.Title}  [{ap.Status}]");

        // 4. Combined: date range AND status filter
        var pending2024plus = await _db.Appointments.WhereAsync(
            a => a.Status == "Pending" &&
                 a.Date.CompareTo("2025-01-01") >= 0);

        Print($"\n  [4] Pending appointments in 2025+: {pending2024plus.Count()} found");
        foreach (var ap in pending2024plus)
            Print($"      • {ap.Date} {ap.Time} — {ap.Title}");

        // 5. All appointments ordered by date — using Query() fluent API
        var withTimes = await _db.Appointments
            .Query()
            .Where(x => x.Date.CompareTo("2024-03-15") >= 0 && x.Time.CompareTo("10:00:00") >= 0 &&
                x.Date.CompareTo("2024-04-30") <= 0 && x.Time.CompareTo("15:00:00") <= 0) // optional additional filter
                // .Where(x => SqliteDate.DateTime(x.Date + x.Time).CompareTo("2024-03-15 09:00:00") >= 0 &&
                //       SqliteDate.DateTime(x.Date + x.Time).CompareTo("2024-04-30 08:00:00") < 0)
            .ToListAsync();

        Print($"\n  [5] All appointments ordered by Date ({withTimes.Count()} total):");
        foreach (var ap in withTimes)
            Print($"      • {ap.Date} {ap.Time} — {ap.Title}  [{ap.Status}]");

        // 6. All appointments ordered by date — using Query() fluent API
        var all = await _db.Appointments
            .Query()
            .OrderBy(a => a.Date)
            .ToListAsync();

        Print($"\n  [5] All appointments ordered by Date ({all.Count()} total):");
        foreach (var ap in all)
            Print($"      • {ap.Date} {ap.Time} — {ap.Title}  [{ap.Status}]");

        Print("");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DateTime Today(int hour, int minute)
        => DateTime.Today.AddHours(hour).AddMinutes(minute);

    private static DateTime Yesterday(int hour, int minute)
        => DateTime.Today.AddDays(-1).AddHours(hour).AddMinutes(minute);

    private static DateTime DaysAgo(int days, int hour, int minute)
        => DateTime.Today.AddDays(-days).AddHours(hour).AddMinutes(minute);

    private static DateTime DaysAhead(int days, int hour, int minute)
        => DateTime.Today.AddDays(days).AddHours(hour).AddMinutes(minute);

    private static void Print(string msg) => Console.WriteLine(msg);
}
