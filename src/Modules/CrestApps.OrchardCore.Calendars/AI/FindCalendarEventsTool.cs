using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Calendars.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Calendars.AI;

/// <summary>
/// Lists events on the person's calendars between two times.
/// </summary>
public sealed class FindCalendarEventsTool : CalendarAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "findCalendarEvents";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "from": { "type": "string", "description": "Start of the range, ISO 8601 with an offset, such as 2026-11-02T00:00:00-05:00." },
        "to": { "type": "string", "description": "End of the range, ISO 8601 with an offset. At most 62 days after from." },
        "calendarIds": { "type": "array", "items": { "type": "string" }, "description": "Calendars to read. Leave out for every calendar the user can see." }
      },
      "required": ["from", "to"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Lists the events on the user's calendars between two times, with their ids, times and attendees. Events on calendars the user can only see as busy time have no details.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!DateTimeOffset.TryParse(Read(arguments, "from"), out var from) || !DateTimeOffset.TryParse(Read(arguments, "to"), out var to) || to <= from)
        {
            return "Give from and to as ISO 8601 times, with to after from.";
        }

        var requested = ReadList(arguments, "calendarIds");
        var calendars = (await services.GetRequiredService<ICalendarAccessService>().ListAccessibleAsync(user, CalendarAccessLevel.FreeBusy, cancellationToken: cancellationToken))
            .Where(access => requested.Count == 0 || requested.Contains(access.Calendar.ItemId))
            .ToDictionary(access => access.Calendar.ItemId, access => access.Calendar.Name);

        var occurrences = await services.GetRequiredService<ICalendarQueryService>().GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            CalendarIds = calendars.Keys.ToList(),
            FromUtc = from.UtcDateTime,
            ToUtc = to > from.AddDays(62) ? from.UtcDateTime.AddDays(62) : to.UtcDateTime,
            Viewer = user,
        }, cancellationToken);

        return JsonSerializer.Serialize(occurrences.Take(200).Select(occurrence => occurrence.BusyOnly
            ? (object)new
            {
                calendar = calendars.GetValueOrDefault(occurrence.Event.CalendarId),
                busy = true,
                startUtc = CalendarWorkflowInput.Format(occurrence.StartUtc),
                endUtc = CalendarWorkflowInput.Format(occurrence.EndUtc),
            }
            : new
            {
                eventId = occurrence.IsOverride ? occurrence.Event.ItemId : occurrence.SeriesId,
                occurrenceStart = occurrence.IsRecurring && !occurrence.IsOverride ? CalendarWorkflowInput.Format(occurrence.OriginalStartUtc) : null,
                calendar = calendars.GetValueOrDefault(occurrence.Event.CalendarId),
                title = occurrence.Event.Title,
                allDay = occurrence.Event.AllDay,
                start = occurrence.Start.ToString("yyyy-MM-dd'T'HH:mm"),
                end = occurrence.End.ToString("yyyy-MM-dd'T'HH:mm"),
                timeZone = occurrence.Event.TimeZoneId,
                location = occurrence.Event.Location,
                status = occurrence.Event.Status.ToString(),
                attendees = occurrence.Event.Attendees.Select(attendee => attendee.Name ?? attendee.Email).Where(name => name is not null),
                canChange = occurrence.Event.Editable,
            }), JsonOptions);
    }
}
