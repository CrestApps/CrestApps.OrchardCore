namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the open window for a single day of the week within a business-hours calendar.
/// </summary>
public sealed class BusinessHoursDay
{
    /// <summary>
    /// Gets or sets the day of the week this window applies to.
    /// </summary>
    public DayOfWeek Day { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the queue is open on this day.
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Gets or sets the local time, in minutes from midnight, the open window starts.
    /// </summary>
    public int OpenMinute { get; set; }

    /// <summary>
    /// Gets or sets the local time, in minutes from midnight (exclusive), the open window ends.
    /// </summary>
    public int CloseMinute { get; set; }
}
