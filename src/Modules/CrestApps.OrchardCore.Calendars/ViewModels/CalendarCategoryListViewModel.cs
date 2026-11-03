using CrestApps.OrchardCore.Calendars.Models;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// The categories list.
/// </summary>
public class CalendarCategoryListViewModel
{
    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    public AdminListContext<CalendarCategory> List { get; set; }
}
