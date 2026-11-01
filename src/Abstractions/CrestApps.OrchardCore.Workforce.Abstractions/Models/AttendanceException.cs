using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Attendance that didn't follow the schedule, such as a late start or a missed punch.
/// </summary>
public sealed class AttendanceException : CatalogItem
{
    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the worker's team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets what went wrong.
    /// </summary>
    public AttendanceExceptionKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the day, in the worker's time.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets how many minutes it was off by.
    /// </summary>
    public int Minutes { get; set; }

    /// <summary>
    /// Gets or sets where it stands.
    /// </summary>
    public AttendanceExceptionStatus Status { get; set; } = AttendanceExceptionStatus.Open;

    /// <summary>
    /// Gets or sets the shift.
    /// </summary>
    public string ShiftKey { get; set; }

    /// <summary>
    /// Gets or sets the punch.
    /// </summary>
    public string PunchId { get; set; }

    /// <summary>
    /// Gets or sets the key that finds it again when the timesheet is worked out again.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the points it earns.
    /// </summary>
    public decimal Points { get; set; }

    /// <summary>
    /// Gets or sets a note.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets who excused or confirmed it.
    /// </summary>
    public string DecidedBy { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was excused or confirmed.
    /// </summary>
    public DateTime? DecidedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was raised.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
