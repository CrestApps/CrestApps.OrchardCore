namespace CrestApps.OrchardCore.BusinessHours.ViewModels;

/// <summary>
/// A schedule the picker offers.
/// </summary>
public class HoursSchedulePickerOption
{
    /// <summary>
    /// Gets or sets the schedule id.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the schedule name.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets whether it is chosen.
    /// </summary>
    public bool Selected { get; set; }

    /// <summary>
    /// Gets or sets whether the chosen schedule is no longer offered: disabled or deleted.
    /// </summary>
    public bool Missing { get; set; }
}
