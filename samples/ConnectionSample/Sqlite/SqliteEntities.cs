using System.ComponentModel.DataAnnotations;

namespace ConnectionSample.Sqlite;

/// <summary>
/// Entity whose date is stored as a CLR <see cref="DateTime"/>.
/// Microsoft.Data.Sqlite serialises DateTime columns as ISO-8601 TEXT
/// ("YYYY-MM-DD HH:MM:SS"), which SQLite's date() / datetime() functions understand.
/// Used to test SqliteDate.Date() and SqliteDate.DateTime() markers.
/// </summary>
public class CalendarEvent
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Stored by Dapper as "YYYY-MM-DD HH:MM:SS" TEXT.
    /// Use SqliteDate.Date(e.OccursAt) to filter on the date part only.
    /// </summary>
    public DateTime OccursAt { get; set; }

    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Entity whose date and time are stored as separate <see langword="string"/> columns
/// in "YYYY-MM-DD" and "HH:MM:SS" format respectively.
/// Used to test string.CompareTo / string.Compare range queries.
/// </summary>
public class Appointment
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Stored as "YYYY-MM-DD" string.</summary>
    [MaxLength(10)]
    public string Date { get; set; } = string.Empty;

    /// <summary>Stored as "HH:MM:SS" string.</summary>
    [MaxLength(8)]
    public string Time { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Status { get; set; } = "Pending";
}
