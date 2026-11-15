using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Team members who work to a Workforce schedule and are off it now: they have published shifts within a week either
/// side of now but none covering now, or they are in a break, meal, meeting, training or other time away inside their
/// shift. A user with no shifts around now isn't scheduled by Workforce, so this source says nothing about them.
/// </summary>
internal sealed class WorkforceShiftAvailabilitySource : ITeamMemberAvailabilitySource
{
    /// <inheritdoc/>
    public string Name => "WorkforceShifts";

    /// <inheritdoc/>
    public global::OrchardCore.Localization.LocalizationSource DisplayName { get; } = global::OrchardCore.Localization.LocalizationSource.Create<WorkforceShiftAvailabilitySource>("On a published shift and not on a break");

    // How far around now a shift shows that a user works to a schedule.
    private static readonly TimeSpan _scheduledWindow = TimeSpan.FromDays(7);

    private readonly ICalendarQueryService _queryService;
    private readonly IClock _clock;

    public WorkforceShiftAvailabilitySource(ICalendarQueryService queryService, IClock clock)
    {
        _queryService = queryService;
        _clock = clock;
    }

    public async Task<IReadOnlyCollection<string>> GetUnavailableAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds?.Where(id => !string.IsNullOrEmpty(id)).ToHashSet(StringComparer.Ordinal) ?? [];

        if (ids.Count == 0)
        {
            return [];
        }

        var now = _clock.UtcNow;
        var occurrences = await _queryService.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            AttendeeUserIds = [.. ids],
            FromUtc = now - _scheduledWindow,
            ToUtc = now + _scheduledWindow,
            EventTypes = [WorkforceConstants.EventTypes.Shift],
        }, cancellationToken);

        var scheduled = new HashSet<string>(StringComparer.Ordinal);
        var working = new HashSet<string>(StringComparer.Ordinal);

        foreach (var occurrence in occurrences)
        {
            if (occurrence.Event.Status == CalendarEventStatus.Cancelled)
            {
                continue;
            }

            var coversNow = occurrence.StartUtc <= now && now < occurrence.EndUtc;
            var awayInside = coversNow
                && occurrence.Event.TryGet<ShiftData>(out var shift)
                && shift.Segments.Any(segment => ShiftSegmentBusyTimeSource.BusyKinds.Contains(segment.Kind) && segment.StartUtc <= now && now < segment.EndUtc);

            foreach (var attendee in occurrence.Event.Attendees ?? [])
            {
                if (string.IsNullOrEmpty(attendee?.UserId) || !ids.Contains(attendee.UserId))
                {
                    continue;
                }

                scheduled.Add(attendee.UserId);

                if (coversNow && !awayInside)
                {
                    working.Add(attendee.UserId);
                }
            }
        }

        return [.. scheduled.Where(userId => !working.Contains(userId))];
    }
}
