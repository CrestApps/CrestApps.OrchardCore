namespace CrestApps.OrchardCore.BusinessHours.ViewModels;

/// <summary>
/// The schedule picker.
/// </summary>
public class HoursSchedulePickerViewModel
{
    /// <summary>
    /// Gets or sets the element id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the input name.
    /// </summary>
    public string InputName { get; set; }

    /// <summary>
    /// Gets or sets whether a choice is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the text of the empty choice.
    /// </summary>
    public string EmptyText { get; set; }

    /// <summary>
    /// Gets or sets the schedules.
    /// </summary>
    public IList<HoursSchedulePickerOption> Options { get; set; } = [];
}
