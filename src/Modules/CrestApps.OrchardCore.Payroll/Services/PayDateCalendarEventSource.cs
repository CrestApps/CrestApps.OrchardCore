using System.Security.Claims;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Pay dates on the calendar, read-only: a person who may see pay runs sees the pay date of each run of their employers;
/// a staff member who may view their own pay sees their own coming pay dates (from their pay schedules) on their
/// personal calendar. Pay dates take no time, so nothing is busy.
/// </summary>
internal sealed class PayDateCalendarEventSource : ICalendarEventSource
{
    public const string SourceName = "Payroll.PayDates";

    public const string EventType = "PayDate";

    public const string Color = "#2f9e44";

    private readonly PayrollReportReader _reader;
    private readonly IPayProfileManager _profiles;
    private readonly IPayScheduleManager _schedules;
    private readonly IStaffDirectory _staff;
    private readonly ICalendarService _calendars;
    private readonly IAuthorizationService _authorizationService;
    private readonly PayrollEmployerOptions _employerOptions;

    internal readonly IStringLocalizer S;

    public PayDateCalendarEventSource(
        PayrollReportReader reader,
        IPayProfileManager profiles,
        IPayScheduleManager schedules,
        IStaffDirectory staff,
        ICalendarService calendars,
        IAuthorizationService authorizationService,
        IOptions<PayrollEmployerOptions> employerOptions,
        IStringLocalizer<PayDateCalendarEventSource> stringLocalizer)
    {
        _reader = reader;
        _profiles = profiles;
        _schedules = schedules;
        _staff = staff;
        _calendars = calendars;
        _authorizationService = authorizationService;
        _employerOptions = employerOptions.Value;
        S = stringLocalizer;
    }

    public string Name => SourceName;

    public async Task<IEnumerable<CalendarOccurrence>> GetOccurrencesAsync(CalendarOccurrenceQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Pay dates are shown to a person, never used as busy time.
        if (query.Viewer is null
            || (query.Sources.Count > 0 && !query.Sources.Contains(SourceName, StringComparer.OrdinalIgnoreCase))
            || (query.EventTypes.Count > 0 && !query.EventTypes.Contains(EventType, StringComparer.OrdinalIgnoreCase)))
        {
            return [];
        }

        var from = DateOnly.FromDateTime(query.FromUtc);
        var to = DateOnly.FromDateTime(query.ToUtc);

        if (to < from)
        {
            return [];
        }

        var occurrences = new List<CalendarOccurrence>();

        // The pay runs of the employers the viewer may see.
        if (query.AllCalendars)
        {
            var scope = await _reader.GetScopeAsync(query.Viewer, PayrollPermissions.ViewPayRuns, cancellationToken);
            var employers = await _reader.EmployerNamesAsync(cancellationToken);

            foreach (var run in (await _reader.ListRunsAsync(scope, from, to, cancellationToken)).Where(run => run.Status != PayRunStatus.Canceled))
            {
                occurrences.Add(Occurrence($"run:{run.ItemId}", null, run.PayDate, (_employerOptions.MultipleEmployers
                    ? S["Pay day: {0} ({1})", PayRunTitles.Name(run, S), employers.GetValueOrDefault(run.EmployerId) ?? run.EmployerId]
                    : S["Pay day: {0}", PayRunTitles.Name(run, S)]).Value));
            }
        }

        // The viewer's own pay dates, on their personal calendar.
        var viewerId = query.Viewer.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrEmpty(viewerId) && await _authorizationService.AuthorizeAsync(query.Viewer, PayrollPermissions.ViewOwnPay)
            && await _staff.FindByUserIdAsync(viewerId, cancellationToken) is { } me)
        {
            var personal = query.CalendarIds.Count == 0
                ? null
                : (await _calendars.GetAsync(query.CalendarIds, cancellationToken)).FirstOrDefault(calendar => calendar.OwnerKind == CalendarOwnerKind.User && calendar.OwnerId == viewerId);

            if (personal is not null || query.AllCalendars || query.AttendeeUserIds.Contains(viewerId))
            {
                foreach (var profile in (await _profiles.ListByStaffMemberAsync(me.ItemId, cancellationToken)).Where(profile => profile.Status == PayProfileStatus.Active && !string.IsNullOrEmpty(profile.PayScheduleId)))
                {
                    if (await _schedules.FindByIdAsync(profile.PayScheduleId, cancellationToken) is not { } schedule)
                    {
                        continue;
                    }

                    foreach (var payDate in PayDates(schedule, from, to))
                    {
                        occurrences.Add(Occurrence($"own:{profile.ItemId}:{payDate:yyyyMMdd}", personal?.ItemId, payDate, S["Pay day"].Value));
                    }
                }
            }
        }

        return occurrences;
    }

    // The pay dates of a schedule's periods that fall in a range of days.
    internal static IEnumerable<DateOnly> PayDates(PaySchedule schedule, DateOnly from, DateOnly to)
    {
        var seen = new HashSet<DateOnly>();
        var day = from.AddDays(-31);

        while (day <= to)
        {
            var (_, end) = schedule.PeriodOn(day);
            var payDate = schedule.PayDateFor(end);

            if (payDate >= from && payDate <= to && seen.Add(payDate))
            {
                yield return payDate;
            }

            day = end.AddDays(1);
        }
    }

    private static CalendarOccurrence Occurrence(string id, string calendarId, DateOnly payDate, string title)
    {
        var start = payDate.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        var calendarEvent = new CalendarEvent
        {
            ItemId = id,
            CalendarId = calendarId,
            EventType = EventType,
            Title = title,
            AllDay = true,
            Start = start,
            End = end,
            TimeZoneId = "UTC",
            StartUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc),
            EndUtc = DateTime.SpecifyKind(end, DateTimeKind.Utc),
            Status = CalendarEventStatus.Confirmed,
            Transparency = CalendarTransparency.Free,
            Color = Color,
            Source = SourceName,
            SourceId = id,
            Editable = false,
        };

        return new CalendarOccurrence
        {
            Event = calendarEvent,
            SeriesId = id,
            OriginalStartUtc = calendarEvent.StartUtc,
            Start = start,
            End = end,
            StartUtc = calendarEvent.StartUtc,
            EndUtc = calendarEvent.EndUtc,
        };
    }
}
