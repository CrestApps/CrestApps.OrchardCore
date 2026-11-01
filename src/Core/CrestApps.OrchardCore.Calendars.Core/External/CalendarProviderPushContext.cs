using CrestApps.OrchardCore.Calendars.Models;

namespace CrestApps.OrchardCore.Calendars.Core.External;

/// <summary>
/// What <see cref="IWritableCalendarProvider"/> writes.
/// </summary>
public sealed class CalendarProviderPushContext
{
    /// <summary>
    /// Gets or sets the calendar.
    /// </summary>
    public Calendar Calendar { get; set; }

    /// <summary>
    /// Gets or sets the subscription.
    /// </summary>
    public CalendarSubscription Subscription { get; set; }

    /// <summary>
    /// Gets or sets the event to write, or <see langword="null"/> for a delete. An event with a
    /// <see cref="CalendarEvent.SeriesId"/> changes one occurrence of the repeating event <see cref="SeriesMapping"/> points to.
    /// </summary>
    public CalendarEvent Event { get; set; }

    /// <summary>
    /// Gets or sets the external copy, or <see langword="null"/> when the event has none yet. Its
    /// <see cref="CalendarProviderMapping.Version"/> is <see langword="null"/> when the write must overwrite whatever is there,
    /// because the local copy won a conflict.
    /// </summary>
    public CalendarProviderMapping Mapping { get; set; }

    /// <summary>
    /// Gets or sets the external copy of the repeating event an occurrence change belongs to.
    /// </summary>
    public CalendarProviderMapping SeriesMapping { get; set; }
}
