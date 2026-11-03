namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// A calendar type someone may create a calendar of.
/// </summary>
public class CalendarTypeOption
{
    /// <summary>
    /// Gets or sets the type's technical name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name shown.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets what calendars of the type are for.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the type's icon.
    /// </summary>
    public string Icon { get; set; }
}
