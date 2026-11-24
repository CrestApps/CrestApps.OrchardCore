namespace CrestApps.OrchardCore.BusinessHours.Core.Models;

/// <summary>
/// One day of the week as the weekly hours editor shows it: open or closed, and up to two open windows, so a lunch
/// break can be entered. Times are local, in <c>HH:mm</c>.
/// </summary>
public class WeeklyHoursRow
{
    /// <summary>
    /// Gets or sets the day.
    /// </summary>
    public DayOfWeek Day { get; set; }

    /// <summary>
    /// Gets or sets the localized name of the day, for display.
    /// </summary>
    public string DayName { get; set; }

    /// <summary>
    /// Gets or sets whether the day is open.
    /// </summary>
    public bool Open { get; set; }

    /// <summary>
    /// Gets or sets when the first window opens.
    /// </summary>
    public string From { get; set; }

    /// <summary>
    /// Gets or sets when the first window closes.
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets when the second window opens; empty when the day has one window.
    /// </summary>
    public string SecondFrom { get; set; }

    /// <summary>
    /// Gets or sets when the second window closes; empty when the day has one window.
    /// </summary>
    public string SecondTo { get; set; }
}
