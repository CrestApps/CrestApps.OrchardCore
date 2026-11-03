using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;

namespace CrestApps.OrchardCore.Calendars.Core.External;

/// <summary>
/// Notes local changes to calendars with a two-way subscription, so the next synchronization run comes early and writes them
/// back, and remembers the external copies of deleted events so it can delete them too.
/// </summary>
internal sealed class CalendarSyncChangeObserver : ICalendarEventObserver
{
    private readonly ICalendarService _calendarService;
    private readonly CalendarSyncService _syncService;

    public CalendarSyncChangeObserver(
        ICalendarService calendarService,
        CalendarSyncService syncService)
    {
        _calendarService = calendarService;
        _syncService = syncService;
    }

    public async Task OnChangedAsync(CalendarEventChange change, CancellationToken cancellationToken = default)
    {
        if (change?.Event is null ||
            string.Equals(change.Actor, CalendarSyncService.Source, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(change.Event.CalendarId) ||
            !(change.Event.Source is null || string.Equals(change.Event.Source, CalendarSyncService.Source, StringComparison.Ordinal)))
        {
            return;
        }

        var calendar = await _calendarService.FindByIdAsync(change.Event.CalendarId, cancellationToken);

        if (calendar is null ||
            !calendar.TryGet<CalendarSubscription>(out var subscription) ||
            !subscription.Enabled ||
            !subscription.TwoWay ||
            _syncService.FindProvider(subscription.Provider) is not IWritableCalendarProvider provider)
        {
            return;
        }

        if (change.Kind == CalendarEventChangeKind.Deleted)
        {
            var mapping = CalendarSyncService.FindMapping(change.Event, calendar, provider);

            if (mapping is null)
            {
                return;
            }

            if (!subscription.PendingDeletes.Any(item => string.Equals(item.ExternalEventId, mapping.ExternalEventId, StringComparison.Ordinal)))
            {
                subscription.PendingDeletes.Add(mapping);
            }
        }

        subscription.PendingPush = true;
        calendar.Put(subscription);

        await _calendarService.UpdateAsync(calendar, new CalendarChangeOptions { Actor = CalendarSyncService.Source }, cancellationToken);
    }
}
