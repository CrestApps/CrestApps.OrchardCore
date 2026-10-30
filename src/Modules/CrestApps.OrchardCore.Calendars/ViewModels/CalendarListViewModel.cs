using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// The list of calendars.
/// </summary>
public class CalendarListViewModel
{
    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the calendar type to show, or empty for every type.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the list.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }

    /// <summary>
    /// Gets or sets the calendar types the viewer may create.
    /// </summary>
    [BindNever]
    public IList<CalendarTypeOption> CreatableTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the calendar types to filter by.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TypeOptions { get; set; } = [];
}
