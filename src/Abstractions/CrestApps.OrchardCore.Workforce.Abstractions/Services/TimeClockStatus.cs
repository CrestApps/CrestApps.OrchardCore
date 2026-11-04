using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Where a worker stands on the clock, and what they can punch next.
/// </summary>
public sealed class TimeClockStatus
{
    /// <summary>
    /// Gets or sets where they stand.
    /// </summary>
    public TimeClockState State { get; set; }

    /// <summary>
    /// Gets or sets since when.
    /// </summary>
    public DateTime? SinceUtc { get; set; }

    /// <summary>
    /// Gets or sets their last punch.
    /// </summary>
    public TimePunch LastPunch { get; set; }

    /// <summary>
    /// Gets or sets the punches they can make now.
    /// </summary>
    public IReadOnlyList<TimePunchKind> NextPunches { get; set; } = [];

    /// <summary>
    /// Gets or sets their policy; <see langword="null"/> when their team has none.
    /// </summary>
    public TimeClockPolicy Policy { get; set; }

    /// <summary>
    /// Gets or sets whether their policy asks for their location.
    /// </summary>
    public bool NeedsLocation { get; set; }

    /// <summary>
    /// Gets or sets the worker's time zone.
    /// </summary>
    public string TimeZoneId { get; set; }
}
