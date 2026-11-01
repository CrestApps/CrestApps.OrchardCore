using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// A timesheet worked out by <see cref="ITimesheetCalculator"/>.
/// </summary>
public sealed class TimesheetCalculation
{
    /// <summary>
    /// Gets the lines.
    /// </summary>
    public List<TimesheetLine> Lines { get; } = [];

    /// <summary>
    /// Gets the attendance exceptions, not yet saved.
    /// </summary>
    public List<AttendanceException> Exceptions { get; } = [];

    /// <summary>
    /// Gets the stretches of time from the punches.
    /// </summary>
    public List<TimesheetSpan> Spans { get; } = [];

    /// <summary>
    /// Gets or sets the minutes worked in the period, breaks included and meals not.
    /// </summary>
    public int WorkedMinutes { get; set; }

    /// <summary>
    /// Gets or sets when the worker clocked in, when they are on the clock now.
    /// </summary>
    public DateTime? OnTheClockSinceUtc { get; set; }

    /// <summary>
    /// Gets or sets the time zone the days were worked out in.
    /// </summary>
    public string TimeZoneId { get; set; }
}

/// <summary>
/// A stretch of time between two punches.
/// </summary>
public sealed class TimesheetSpan
{
    /// <summary>
    /// Gets or sets what kind of time it is.
    /// </summary>
    public TimesheetSpanKind Kind { get; set; }

    /// <summary>
    /// Gets or sets when it starts, after rounding and grace.
    /// </summary>
    public DateTime StartUtc { get; set; }

    /// <summary>
    /// Gets or sets when it ends, after rounding and grace.
    /// </summary>
    public DateTime EndUtc { get; set; }

    /// <summary>
    /// Gets or sets the activity.
    /// </summary>
    public string ActivityCodeId { get; set; }

    /// <summary>
    /// Gets or sets the job.
    /// </summary>
    public string Job { get; set; }

    /// <summary>
    /// Gets or sets the punch that started it.
    /// </summary>
    public string StartPunchId { get; set; }

    /// <summary>
    /// Gets or sets the punch that ended it; empty when it was closed at the shift's end.
    /// </summary>
    public string EndPunchId { get; set; }

    /// <summary>
    /// Gets the minutes.
    /// </summary>
    public int Minutes => (int)Math.Round((EndUtc - StartUtc).TotalMinutes);
}

/// <summary>
/// What kind of time a span is.
/// </summary>
public enum TimesheetSpanKind
{
    /// <summary>
    /// Work.
    /// </summary>
    Work,

    /// <summary>
    /// A paid break.
    /// </summary>
    Break,

    /// <summary>
    /// An unpaid meal.
    /// </summary>
    Meal,
}
