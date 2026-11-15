using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Learning.Classes.ViewModels;
using CrestApps.OrchardCore.Scheduling.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Classes.Services;

/// <summary>
/// Converts the meetings of a session between the rows of the session editor, entered in local time, and UTC.
/// </summary>
internal static class ClassSessionMeetings
{
    /// <summary>
    /// Converts the editor rows to UTC meetings. A row without a date is skipped.
    /// </summary>
    public static async Task<ClassMeetingConversion> ToUtcAsync(ILocalClock localClock, IEnumerable<ClassMeetingRow> rows)
    {
        ArgumentNullException.ThrowIfNull(localClock);

        var meetings = new List<SchedulingSessionMeeting>();
        var invalid = new List<int>();
        var index = 0;

        foreach (var row in rows ?? [])
        {
            var position = index++;

            if (row?.Date is not { } date)
            {
                continue;
            }

            if (row.Start is not { } start || row.End is not { } end || end <= start || start < TimeSpan.Zero || end > TimeSpan.FromDays(1))
            {
                invalid.Add(position);

                continue;
            }

            // The times are wall-clock times in the user's or the site's time zone.
            var day = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);

            meetings.Add(new SchedulingSessionMeeting
            {
                StartUtc = DateTime.SpecifyKind(await localClock.ToUtcLenientAsync(day + start), DateTimeKind.Utc),
                EndUtc = DateTime.SpecifyKind(await localClock.ToUtcLenientAsync(day + end), DateTimeKind.Utc),
            });
        }

        return new ClassMeetingConversion(meetings.OrderBy(meeting => meeting.StartUtc).ToList(), invalid);
    }

    /// <summary>
    /// Converts UTC meetings to editor rows in local time.
    /// </summary>
    public static async Task<List<ClassMeetingRow>> ToRowsAsync(ILocalClock localClock, IEnumerable<SchedulingSessionMeeting> meetings)
    {
        ArgumentNullException.ThrowIfNull(localClock);

        var rows = new List<ClassMeetingRow>();

        foreach (var meeting in (meetings ?? []).OrderBy(meeting => meeting.StartUtc))
        {
            var start = (await localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(meeting.StartUtc, DateTimeKind.Utc)))).DateTime;
            var end = (await localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(meeting.EndUtc, DateTimeKind.Utc)))).DateTime;

            rows.Add(new ClassMeetingRow
            {
                Date = start.Date,
                Start = start.TimeOfDay,
                End = end.Date == start.Date ? end.TimeOfDay : TimeSpan.FromDays(1).Subtract(TimeSpan.FromMinutes(1)),
            });
        }

        return rows;
    }
}

/// <summary>
/// The meetings of the editor rows in UTC.
/// </summary>
/// <param name="Meetings">The meetings, soonest first.</param>
/// <param name="InvalidRows">The positions of the rows with a date but no valid start and end.</param>
internal sealed record ClassMeetingConversion(IReadOnlyList<SchedulingSessionMeeting> Meetings, IReadOnlyList<int> InvalidRows);
