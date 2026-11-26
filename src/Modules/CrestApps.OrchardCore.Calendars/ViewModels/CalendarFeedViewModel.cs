using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Calendars.ViewModels;

/// <summary>
/// The subscription address and import pages of a calendar.
/// </summary>
public class CalendarFeedViewModel
{
    /// <summary>
    /// Gets or sets the calendar's id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the calendar's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the subscription address, or <see langword="null"/> when the calendar has none.
    /// </summary>
    public string FeedUrl { get; set; }

    /// <summary>
    /// Gets or sets the import editor, built by the display drivers of this view model.
    /// </summary>
    [BindNever]
    public IShape Editor { get; set; }
}
