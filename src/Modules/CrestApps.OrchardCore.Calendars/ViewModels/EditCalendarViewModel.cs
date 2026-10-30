using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// The page that creates or changes a calendar.
/// </summary>
public class EditCalendarViewModel
{
    /// <summary>
    /// Gets or sets the calendar's id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the calendar's type.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the page title.
    /// </summary>
    [BindNever]
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the editor shape.
    /// </summary>
    [BindNever]
    public dynamic Editor { get; set; }
}
