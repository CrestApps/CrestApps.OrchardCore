namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// One weekly window of a worker's availability, in the worker's time zone.
/// </summary>
public sealed class AvailabilityWindow
{
    /// <summary>
    /// Gets or sets the day of the week.
    /// </summary>
    public DayOfWeek Day { get; set; }

    /// <summary>
    /// Gets or sets when the window starts.
    /// </summary>
    public TimeOnly Start { get; set; }

    /// <summary>
    /// Gets or sets when the window ends; a time at or before the start runs past midnight.
    /// </summary>
    public TimeOnly End { get; set; }

    /// <summary>
    /// Gets or sets how the worker feels about working then.
    /// </summary>
    public AvailabilityPreference Preference { get; set; } = AvailabilityPreference.Available;
}
