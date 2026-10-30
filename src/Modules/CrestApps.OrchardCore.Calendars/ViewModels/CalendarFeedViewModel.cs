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
}
