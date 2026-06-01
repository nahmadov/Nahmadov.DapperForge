using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.Core.Context.Options;
using Nahmadov.DapperForge.Core.Modeling.Builders;
using Nahmadov.DapperForge.Core.Modeling.Schema;

namespace ConnectionSample.Sqlite;

public class SqliteDbContext(DapperDbContextOptions<SqliteDbContext> options) : DapperDbContext(options)
{
    public DapperSet<CalendarEvent> Events => Set<CalendarEvent>();
    public DapperSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(DapperModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CalendarEvent>(b =>
        {
            b.ToTable("CalendarEvents");
            b.Property(e => e.Title).HasMaxLength(200).IsRequired();
            b.Property(e => e.Category).HasMaxLength(50);
        });

        modelBuilder.Entity<Appointment>(b =>
        {
            b.ToTable("Appointments");
            b.Property(a => a.Title).HasMaxLength(200).IsRequired();
            // Explicit column types drive temp-table DDL / bulk copy (Phase 1+).
            b.Property(a => a.Date).HasColumnType(SqlColumnType.Char, 10);
            b.Property(a => a.Time).HasColumnType(SqlColumnType.Char, 8);
            b.Property(a => a.Status).HasMaxLength(50);
        });
    }

    /// <summary>
    /// Creates the SQLite tables for the sample. Called once at startup.
    /// </summary>
    public async Task EnsureSchemaAsync()
    {
        await ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS "CalendarEvents" (
                "Id"       INTEGER PRIMARY KEY AUTOINCREMENT,
                "Title"    TEXT    NOT NULL,
                "OccursAt" TEXT    NOT NULL,
                "Category" TEXT    NOT NULL DEFAULT ''
            );
            """);

        await ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS "Appointments" (
                "Id"     INTEGER PRIMARY KEY AUTOINCREMENT,
                "Title"  TEXT NOT NULL,
                "Date"   TEXT NOT NULL,
                "Time"   TEXT NOT NULL,
                "Status" TEXT NOT NULL DEFAULT 'Pending'
            );
            """);
    }
}
