namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents the open window for a single day of the week in the business-hours calendar editor.
/// </summary>
public class BusinessHoursDayViewModel
{
    /// <summary>
    /// Gets or sets the day of the week.
    /// </summary>
    public DayOfWeek Day { get; set; }

    /// <summary>
    /// Gets or sets the localized display name of the day.
    /// </summary>
    public string DayName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the queue is open on this day.
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Gets or sets the local open time, formatted as <c>HH:mm</c>.
    /// </summary>
    public string OpenTime { get; set; }

    /// <summary>
    /// Gets or sets the local close time, formatted as <c>HH:mm</c>.
    /// </summary>
    public string CloseTime { get; set; }
}
