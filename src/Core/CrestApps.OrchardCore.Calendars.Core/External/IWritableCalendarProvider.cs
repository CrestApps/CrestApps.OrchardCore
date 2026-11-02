namespace CrestApps.OrchardCore.Calendars.Core.External;

/// <summary>
/// A provider that writes local changes back to the external calendar, for a two-way subscription. The synchronization service
/// pushes events that were added or changed here since they were last synchronized and deletes the external copies of events
/// removed here. Pass <see cref="CalendarProviderPushContext.Mapping"/>'s version as a precondition (an <c>If-Match</c> ETag) and
/// throw <see cref="CalendarProviderConflictException"/> when the external copy changed since.
/// </summary>
public interface IWritableCalendarProvider : ICalendarProvider
{
    /// <summary>
    /// Creates the event in the external calendar when <see cref="CalendarProviderPushContext.Mapping"/> is
    /// <see langword="null"/>, or updates its external copy.
    /// </summary>
    /// <param name="context">The event, its calendar and subscription, and its external copy.</param>
    /// <param name="cancellationToken">A token to cancel the write.</param>
    /// <returns>The external copy's id and version.</returns>
    Task<CalendarProviderPushResult> PushAsync(CalendarProviderPushContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an event's external copy. An external copy that is already gone counts as deleted.
    /// </summary>
    /// <param name="context">The calendar, its subscription and the external copy. <see cref="CalendarProviderPushContext.Event"/>
    /// is <see langword="null"/>: the event is already gone here.</param>
    /// <param name="cancellationToken">A token to cancel the write.</param>
    /// <returns>A task.</returns>
    Task DeleteAsync(CalendarProviderPushContext context, CancellationToken cancellationToken = default);
}
