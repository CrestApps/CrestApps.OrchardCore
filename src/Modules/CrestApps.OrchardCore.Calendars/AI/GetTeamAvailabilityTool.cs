using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.ViewModels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// Reads a team calendar's day as the Team Availability page does: each member's busy time and the times everyone is free.
/// </summary>
internal sealed class GetTeamAvailabilityTool : CalendarAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "getTeamAvailability";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "calendarId": { "type": "string", "description": "The Team calendar's ID, from listCalendars." },
        "date": { "type": "string", "description": "The day, yyyy-MM-dd, in the calendar's time zone. Defaults to today." },
        "minutes": { "type": "integer", "minimum": 15, "maximum": 480, "description": "The length of the meeting to find free times for, 30 by default." }
      },
      "required": ["calendarId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "For a team calendar and a day, lists each team member's busy times and the times everyone is free for a meeting of the given length, between 07:00 and 19:00 in the calendar's time zone. Members are the users in the team's role, up to 50. Needs the Team Calendars feature.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (calendar, level) = await CalendarToolSupport.FindCalendarAsync(services, user, Read(arguments, "calendarId"), cancellationToken);

        if (calendar is null)
        {
            return CalendarToolSupport.CalendarNotFound;
        }

        if (!string.Equals(calendar.Type, CalendarsConstants.Types.Team, StringComparison.OrdinalIgnoreCase))
        {
            return "That calendar is not a Team calendar. Use findFreeTime for other calendars.";
        }

        if (level < CalendarAccessLevel.Read)
        {
            return "The user can't read that team calendar.";
        }

        DateTime? day = null;

        if (Read(arguments, "date") is { } date)
        {
            if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return "Give the date as yyyy-MM-dd.";
            }

            day = parsed;
        }

        // The hours and limits of the Team Availability page.
        var page = new TeamAvailabilityViewModel();
        var minutes = Math.Clamp(CalendarToolSupport.ReadInt(arguments, "minutes") ?? page.Minutes, 15, 480);
        var availability = await services.GetRequiredService<TeamAvailabilityService>().GetAsync(calendar, day, minutes, page.FirstHour, page.LastHour, cancellationToken);
        var zone = availability.TimeZoneId;

        return CalendarToolSupport.Json(new
        {
            calendarId = calendar.ItemId,
            calendar = calendar.Name,
            role = calendar.OwnerId,
            date = availability.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            timeZone = zone,
            from = CalendarToolSupport.FormatWallClock(CalendarTime.ToLocal(availability.FromUtc, zone), allDay: false),
            to = CalendarToolSupport.FormatWallClock(CalendarTime.ToLocal(availability.ToUtc, zone), allDay: false),
            meetingMinutes = minutes,
            members = availability.Members.Select(member => new
            {
                userId = member.UserId,
                userName = member.UserName,
                busy = member.Busy.OrderBy(interval => interval.StartUtc).Select(interval => new
                {
                    start = CalendarToolSupport.FormatWallClock(CalendarTime.ToLocal(interval.StartUtc, zone), allDay: false),
                    end = CalendarToolSupport.FormatWallClock(CalendarTime.ToLocal(interval.EndUtc, zone), allDay: false),
                    tentative = interval.Tentative,
                }),
            }),
            membersCapped = availability.Members.Count >= TeamAvailabilityService.MaxMembers,
            freeTimes = availability.FreeTimes.Select(window => new
            {
                start = CalendarToolSupport.FormatWallClock(CalendarTime.ToLocal(window.StartUtc, zone), allDay: false),
                end = CalendarToolSupport.FormatWallClock(CalendarTime.ToLocal(window.EndUtc, zone), allDay: false),
            }),
        });
    }
}
