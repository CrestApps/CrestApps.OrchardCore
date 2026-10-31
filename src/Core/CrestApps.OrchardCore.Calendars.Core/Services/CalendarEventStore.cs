using CrestApps.OrchardCore.Calendars.Core.Indexes;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Calendars.Core.Services;

/// <summary>
/// Stores events as YesSql documents in the <see cref="CalendarsConstants.CollectionName"/> collection.
/// </summary>
public sealed class CalendarEventStore : DocumentCatalog<CalendarEvent, CalendarEventIndex>, ICalendarEventStore
{
    // Two people moving the same event must not overwrite each other's changes.
    protected override bool CheckConcurrency => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public CalendarEventStore(ISession session)
        : base(session)
    {
        CollectionName = CalendarsConstants.CollectionName;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarEvent>> ListInRangeAsync(IEnumerable<string> calendarIds, DateTime fromUtc, DateTime toUtc, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendarIds);

        var ids = calendarIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        var events = await Session.Query<CalendarEvent, CalendarEventIndex>(
                index => index.CalendarId.IsIn(ids) && index.StartUtc < toUtc && index.RangeEndUtc >= fromUtc,
                collection: CollectionName)
            .OrderBy(index => index.StartUtc)
            .Take(limit)
            .ListAsync(cancellationToken);

        return await LoadedAsync(events);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarEvent>> ListAttendingInRangeAsync(IEnumerable<string> userIds, IEnumerable<string> resourceCalendarIds, DateTime fromUtc, DateTime toUtc, int limit, CancellationToken cancellationToken = default)
    {
        var users = userIds?.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray() ?? [];
        var resources = resourceCalendarIds?.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray() ?? [];
        var results = new Dictionary<string, CalendarEvent>(StringComparer.Ordinal);

        if (users.Length > 0)
        {
            var events = await Session.Query<CalendarEvent, CalendarEventAttendeeIndex>(
                    index => index.UserId.IsIn(users) && index.Response != CalendarAttendeeResponse.Declined && index.StartUtc < toUtc && index.RangeEndUtc >= fromUtc,
                    collection: CollectionName)
                .Take(limit)
                .ListAsync(cancellationToken);

            Add(results, events);
        }

        if (resources.Length > 0)
        {
            var events = await Session.Query<CalendarEvent, CalendarEventAttendeeIndex>(
                    index => index.ResourceCalendarId.IsIn(resources) && index.Response != CalendarAttendeeResponse.Declined && index.StartUtc < toUtc && index.RangeEndUtc >= fromUtc,
                    collection: CollectionName)
                .Take(limit)
                .ListAsync(cancellationToken);

            Add(results, events);
        }

        return await LoadedAsync(results.Values);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarEvent>> ListOverridesAsync(IEnumerable<string> seriesIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seriesIds);

        var ids = seriesIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray();

        if (ids.Length == 0)
        {
            return [];
        }

        var events = await Session.Query<CalendarEvent, CalendarEventIndex>(index => index.SeriesId.IsIn(ids), collection: CollectionName)
            .ListAsync(cancellationToken);

        return await LoadedAsync(events);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarEvent>> ListBySourceAsync(string source, string sourceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(sourceId))
        {
            return [];
        }

        var events = await Session.Query<CalendarEvent, CalendarEventIndex>(index => index.Source == source && index.SourceId == sourceId, collection: CollectionName)
            .ListAsync(cancellationToken);

        return await LoadedAsync(events);
    }

    /// <inheritdoc />
    public async Task<CalendarEvent> FindByUidAsync(string calendarId, string uid, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(calendarId) || string.IsNullOrEmpty(uid))
        {
            return null;
        }

        var calendarEvent = await Session.Query<CalendarEvent, CalendarEventIndex>(index => index.CalendarId == calendarId && index.Uid == uid && index.SeriesId == null, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

        return await LoadedAsync(calendarEvent);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarEvent>> ListByCalendarAsync(string calendarId, int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(calendarId);

        var events = await Session.Query<CalendarEvent, CalendarEventIndex>(index => index.CalendarId == calendarId, collection: CollectionName)
            .OrderBy(index => index.StartUtc)
            .ThenBy(index => index.Id)
            .Skip(skip)
            .Take(take)
            .ListAsync(cancellationToken);

        return await LoadedAsync(events);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarEvent>> ListAnyInRangeAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken cancellationToken = default)
    {
        var events = await Session.Query<CalendarEvent, CalendarEventIndex>(
                index => index.StartUtc < toUtc && index.RangeEndUtc >= fromUtc && index.Status != CalendarEventStatus.Cancelled,
                collection: CollectionName)
            .OrderBy(index => index.StartUtc)
            .Take(limit)
            .ListAsync(cancellationToken);

        return await LoadedAsync(events);
    }

    private static void Add(Dictionary<string, CalendarEvent> results, IEnumerable<CalendarEvent> events)
    {
        foreach (var calendarEvent in events)
        {
            results.TryAdd(calendarEvent.ItemId, calendarEvent);
        }
    }
}
