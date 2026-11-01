using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Writes and reads the Shift events Workforce puts on team calendars, for publishing, open shifts and trades.
/// </summary>
internal static class ShiftEvents
{
    /// <summary>
    /// Returns a live Workforce shift's data, or <see langword="false"/> when the event isn't one.
    /// </summary>
    public static bool TryRead(CalendarEvent calendarEvent, out ShiftData data)
    {
        data = null;

        return calendarEvent is not null
            && calendarEvent.EventType == WorkforceConstants.EventTypes.Shift
            && calendarEvent.Source == WorkforceConstants.Source
            && calendarEvent.Status != CalendarEventStatus.Cancelled
            && calendarEvent.TryGet(out data)
            && !string.IsNullOrEmpty(data.WorkerId);
    }

    /// <summary>
    /// Builds the shift data of a shift, with its segments in UTC.
    /// </summary>
    public static ShiftData ToShiftData(string workerId, string teamId, string scheduleId, string templateId, string shiftKey, int version, IEnumerable<DraftShiftSegment> segments, IReadOnlyDictionary<string, ActivityCode> codes, string timeZoneId)
    {
        return new ShiftData
        {
            WorkerId = workerId,
            TeamId = teamId,
            ScheduleId = scheduleId,
            ShiftTemplateId = templateId,
            ShiftKey = shiftKey,
            PublishedVersion = version,
            Segments = (segments ?? []).Select(segment => new ShiftSegment
            {
                ActivityCodeId = segment.ActivityCodeId,
                Kind = segment.ActivityCodeId is not null && codes.TryGetValue(segment.ActivityCodeId, out var code) ? code.Kind : ActivityKind.Break,
                StartUtc = CalendarTime.ToUtc(segment.Start, timeZoneId),
                EndUtc = CalendarTime.ToUtc(segment.End, timeZoneId),
            }).ToList(),
        };
    }

    /// <summary>
    /// Writes a shift onto an event: its worker, times, notes and data.
    /// </summary>
    public static void Apply(CalendarEvent calendarEvent, WorkerProfile worker, DateTime start, DateTime end, string notes, ShiftData data, string timeZoneId)
    {
        calendarEvent.AllDay = false;
        calendarEvent.TimeZoneId = timeZoneId;
        calendarEvent.Start = start;
        calendarEvent.End = end;
        calendarEvent.Description = notes;
        Assign(calendarEvent, worker);
        calendarEvent.Put(data);
    }

    /// <summary>
    /// Names a worker on an event, as its title and, when they have a user account or an email, its attendee.
    /// </summary>
    public static void Assign(CalendarEvent calendarEvent, WorkerProfile worker)
    {
        calendarEvent.Title = worker.DisplayName;

        // A worker without a user account or an email can't be an attendee; the shift data still names them.
        calendarEvent.Attendees = string.IsNullOrEmpty(worker.UserId) && string.IsNullOrEmpty(worker.Email)
            ? []
            :
            [
                new CalendarAttendee
                {
                    UserId = worker.UserId,
                    Email = worker.Email,
                    Name = worker.DisplayName,
                    Role = CalendarAttendeeRole.Required,
                    Response = CalendarAttendeeResponse.Accepted,
                },
            ];
    }

    /// <summary>
    /// Returns a published shift's paid minutes: its length less its unpaid segments.
    /// </summary>
    public static int PaidMinutes(CalendarEvent calendarEvent, ShiftData data, IReadOnlyDictionary<string, ActivityCode> codes)
    {
        var unpaid = data.Segments
            .Where(segment => IsUnpaid(segment.ActivityCodeId, segment.Kind, codes))
            .Sum(segment => (segment.EndUtc - segment.StartUtc).TotalMinutes);

        return (int)Math.Max(0, (calendarEvent.EndUtc - calendarEvent.StartUtc).TotalMinutes - unpaid);
    }

    /// <summary>
    /// Returns the paid minutes of a published shift that fall inside a range: its time in the range less the unpaid
    /// segments in the range.
    /// </summary>
    public static int PaidMinutesWithin(CalendarEvent calendarEvent, ShiftData data, IReadOnlyDictionary<string, ActivityCode> codes, DateTime fromUtc, DateTime toUtc)
    {
        var unpaid = data.Segments
            .Where(segment => IsUnpaid(segment.ActivityCodeId, segment.Kind, codes))
            .Sum(segment => Overlap(segment.StartUtc, segment.EndUtc, fromUtc, toUtc));

        return (int)Math.Max(0, Overlap(calendarEvent.StartUtc, calendarEvent.EndUtc, fromUtc, toUtc) - unpaid);
    }

    /// <summary>
    /// Returns a planned shift's paid minutes: its length less its unpaid segments.
    /// </summary>
    public static int PaidMinutes(DateTime start, DateTime end, IEnumerable<DraftShiftSegment> segments, IReadOnlyDictionary<string, ActivityCode> codes)
    {
        var unpaid = (segments ?? [])
            .Where(segment => IsUnpaid(segment.ActivityCodeId, null, codes))
            .Sum(segment => (segment.End - segment.Start).TotalMinutes);

        return (int)Math.Max(0, (end - start).TotalMinutes - unpaid);
    }

    private static double Overlap(DateTime start, DateTime end, DateTime fromUtc, DateTime toUtc)
    {
        var from = start > fromUtc ? start : fromUtc;
        var to = end < toUtc ? end : toUtc;

        return to > from ? (to - from).TotalMinutes : 0;
    }

    private static bool IsUnpaid(string activityCodeId, ActivityKind? kind, IReadOnlyDictionary<string, ActivityCode> codes)
    {
        if (activityCodeId is not null && codes.TryGetValue(activityCodeId, out var code))
        {
            return !code.IsPaid;
        }

        return kind == ActivityKind.Meal;
    }
}
