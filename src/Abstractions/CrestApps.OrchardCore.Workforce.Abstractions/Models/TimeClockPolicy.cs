using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// How a team's time is clocked and paid: rounding, grace, meals, where workers may punch from, overtime, the pay
/// periods and attendance points. Teams use their own policy or their parent team's.
/// </summary>
public sealed class TimeClockPolicy : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the policy name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what the policy is for.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the pay periods the policy's timesheets follow.
    /// </summary>
    public string PayPeriodCalendarId { get; set; }

    /// <summary>
    /// Gets or sets the minutes punch times round to: 0 for none, or 1, 5, 6 or 15.
    /// </summary>
    public int RoundingMinutes { get; set; }

    /// <summary>
    /// Gets or sets which way punch times round.
    /// </summary>
    public TimeRoundingDirection RoundingDirection { get; set; } = TimeRoundingDirection.Nearest;

    /// <summary>
    /// Gets or sets how many minutes before the shift a worker may clock in and still be paid from the shift's start.
    /// </summary>
    public int EarlyGraceMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets how many minutes late a worker may clock in, or early they may clock out, before it counts.
    /// </summary>
    public int LateGraceMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets how many minutes before a shift clocking in is refused; <see langword="null"/> to allow it any time.
    /// </summary>
    public int? BlockEarlyPunchInMinutes { get; set; }

    /// <summary>
    /// Gets or sets the hours of work in a day after which a meal is taken off when the worker didn't punch one;
    /// <see langword="null"/> for none.
    /// </summary>
    public decimal? AutoMealAfterHours { get; set; }

    /// <summary>
    /// Gets or sets the length of the meal taken off.
    /// </summary>
    public int AutoMealMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets the longest meal before it counts as long; <see langword="null"/> for no limit.
    /// </summary>
    public int? LongMealMinutes { get; set; }

    /// <summary>
    /// Gets or sets where workers may punch from; empty for anywhere.
    /// </summary>
    public IList<TimePunchSource> AllowedSources { get; set; } = [];

    /// <summary>
    /// Gets or sets whether workers must punch at the work site, within <see cref="GeofenceRadiusMeters"/> of their
    /// team's location.
    /// </summary>
    public bool RequireGeofence { get; set; }

    /// <summary>
    /// Gets or sets how far from the team's location a punch counts as at the site.
    /// </summary>
    public int GeofenceRadiusMeters { get; set; } = 200;

    /// <summary>
    /// Gets or sets what happens when a worker forgets to clock out.
    /// </summary>
    public MissedPunchHandling MissedPunchHandling { get; set; } = MissedPunchHandling.Flag;

    /// <summary>
    /// Gets or sets the hours a day after which hours are overtime; <see langword="null"/> for none.
    /// </summary>
    public decimal? DailyOvertimeHours { get; set; }

    /// <summary>
    /// Gets or sets the hours a day after which hours are double time; <see langword="null"/> for none.
    /// </summary>
    public decimal? DailyDoubleTimeHours { get; set; }

    /// <summary>
    /// Gets or sets the hours a week after which hours are overtime; <see langword="null"/> for none.
    /// </summary>
    public decimal? WeeklyOvertimeHours { get; set; } = 40;

    /// <summary>
    /// Gets or sets the points each kind of attendance exception earns; empty for no points.
    /// </summary>
    public IList<AttendancePointRule> AttendancePoints { get; set; } = [];

    /// <summary>
    /// Gets or sets how many days points count for.
    /// </summary>
    public int PointsWindowDays { get; set; } = 365;

    /// <summary>
    /// Gets or sets the points at which a worker's manager is told; <see langword="null"/> for never.
    /// </summary>
    public decimal? PointsThreshold { get; set; }

    /// <summary>
    /// Gets or sets whether the policy is in use.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time the policy was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the policy was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Returns whether workers may punch from a source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    public bool Allows(TimePunchSource source)
    {
        return source == TimePunchSource.Manager || AllowedSources.Count == 0 || AllowedSources.Contains(source);
    }

    /// <summary>
    /// Returns the points an exception kind earns.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The points, or 0.</returns>
    public decimal PointsFor(AttendanceExceptionKind kind)
    {
        return AttendancePoints.FirstOrDefault(rule => rule.Kind == kind)?.Points ?? 0;
    }
}
