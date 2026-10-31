using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Makes the breaks, meals, meetings and training inside a published shift busy for the worker. A Shift event is free
/// time (time the worker is available for work), so without this the parts of the shift spent away from work could be
/// booked.
/// </summary>
internal sealed class ShiftSegmentBusyTimeSource : ICalendarBusyTimeSource
{
    private static readonly HashSet<ActivityKind> _busyKinds =
    [
        ActivityKind.Break,
        ActivityKind.Meal,
        ActivityKind.Meeting,
        ActivityKind.Training,
        ActivityKind.Coaching,
        ActivityKind.OfflineWork,
        ActivityKind.Travel,
        ActivityKind.TimeOff,
    ];

    private readonly ICalendarQueryService _queryService;

    public ShiftSegmentBusyTimeSource(ICalendarQueryService queryService)
    {
        _queryService = queryService;
    }

    public string Name => WorkforceConstants.Source;

    public async Task<IEnumerable<CalendarBusyInterval>> GetBusyTimeAsync(CalendarBusyQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.UserIds.Count == 0)
        {
            return [];
        }

        var occurrences = await _queryService.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            AttendeeUserIds = query.UserIds,
            FromUtc = query.FromUtc,
            ToUtc = query.ToUtc,
            EventTypes = [WorkforceConstants.EventTypes.Shift],
        }, cancellationToken);

        var users = query.UserIds.ToHashSet(StringComparer.Ordinal);
        var result = new List<CalendarBusyInterval>();

        foreach (var occurrence in occurrences)
        {
            if (!occurrence.Event.TryGet<ShiftData>(out var shift) || query.ExcludeEventIds.Contains(occurrence.Event.ItemId))
            {
                continue;
            }

            foreach (var segment in shift.Segments.Where(segment => _busyKinds.Contains(segment.Kind) && segment.EndUtc > query.FromUtc && segment.StartUtc < query.ToUtc))
            {
                foreach (var attendee in occurrence.Event.Attendees.Where(attendee => !string.IsNullOrEmpty(attendee.UserId) && users.Contains(attendee.UserId)))
                {
                    result.Add(new CalendarBusyInterval
                    {
                        StartUtc = segment.StartUtc,
                        EndUtc = segment.EndUtc,
                        CalendarId = occurrence.Event.CalendarId,
                        UserId = attendee.UserId,
                        EventId = occurrence.Event.ItemId,
                        Source = Name,
                    });
                }
            }
        }

        return result;
    }
}
