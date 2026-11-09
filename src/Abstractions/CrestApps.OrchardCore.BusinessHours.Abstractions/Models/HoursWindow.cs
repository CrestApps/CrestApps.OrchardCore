namespace CrestApps.OrchardCore.BusinessHours.Models;

/// <summary>
/// One open window on a day of the week, in minutes from local midnight.
/// </summary>
/// <remarks>
/// A window that opens and closes at the same minute is open all day. A window that closes before it opens runs past
/// midnight: a Friday window from 22:00 to 06:00 is open Friday night and early Saturday. Minutes outside 0 to 1440 make
/// the window invalid, and it is ignored.
/// </remarks>
public sealed class HoursWindow
{
    /// <summary>
    /// Gets or sets the day of the week the window opens on.
    /// </summary>
    public DayOfWeek Day { get; set; }

    /// <summary>
    /// Gets or sets the local time, in minutes from midnight, the window opens.
    /// </summary>
    public int OpenMinute { get; set; }

    /// <summary>
    /// Gets or sets the local time, in minutes from midnight (exclusive), the window closes.
    /// </summary>
    public int CloseMinute { get; set; }
}
