using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// The General card of the calendar editor.
/// </summary>
public class CalendarGeneralViewModel
{
    /// <summary>
    /// Gets or sets the calendar's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets the time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the visibility.
    /// </summary>
    public string Visibility { get; set; }

    /// <summary>
    /// Gets or sets whether the calendar is in use.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the name of the calendar's type.
    /// </summary>
    [BindNever]
    public string TypeName { get; set; }

    /// <summary>
    /// Gets or sets what the calendar's type is for.
    /// </summary>
    [BindNever]
    public string TypeDescription { get; set; }

    /// <summary>
    /// Gets or sets the icon of the calendar's type.
    /// </summary>
    [BindNever]
    public string TypeIcon { get; set; }

    /// <summary>
    /// Gets or sets the time zones to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TimeZoneOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the visibilities to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> VisibilityOptions { get; set; } = [];
}
