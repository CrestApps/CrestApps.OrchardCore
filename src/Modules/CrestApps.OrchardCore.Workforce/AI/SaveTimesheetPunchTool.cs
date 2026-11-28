using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Adds a punch to a timesheet's worker or corrects one, with a reason, as the timesheet page's Add punch and Correct
/// punch forms do.
/// </summary>
internal sealed class SaveTimesheetPunchTool : TimesheetPunchToolBase
{
    public const string ToolName = "saveTimesheetPunch";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "timesheetId": { "type": "string", "description": "The timesheet's ID, from listTimesheets or getTimesheet." },
        "punchId": { "type": "string", "description": "To correct a punch: its ID, from getTimesheet. Leave out to add a missing punch." },
        "kind": { "type": "string", "enum": ["In", "Out", "MealStart", "MealEnd", "BreakStart", "BreakEnd", "ActivityChange", "JobStart", "JobEnd"], "description": "The punch: clocking in or out, a meal or break starting or ending, or an activity or job change." },
        "date": { "type": "string", "description": "The day of the punch (yyyy-MM-dd), in the worker's time zone." },
        "time": { "type": "string", "description": "The time of the punch (HH:mm), in the worker's time zone (getTimesheet gives it)." },
        "reason": { "type": "string", "description": "Why the punch is added or changed; kept with the punch." }
      },
      "required": ["timesheetId", "kind", "date", "time", "reason"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Adds a missing punch to a worker's timesheet or corrects the kind or time of one, with a reason that is kept. It changes the hours the worker is paid for; a wrong punch can be corrected or removed again.";

    public override JsonElement JsonSchema => _schema;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (timesheet, error) = await TimeClockAdminTools.FindTimesheetAsync(services, user, WorkforceAdminTools.Read(arguments, "timesheetId"), cancellationToken);

        if (error is not null)
        {
            return error;
        }

        if (!WorkforceAdminTools.TryReadEnum<TimePunchKind>(arguments, "kind", out var kind))
        {
            return "The kind must be In, Out, MealStart, MealEnd, BreakStart, BreakEnd, ActivityChange, JobStart or JobEnd.";
        }

        var date = WorkforceAdminTools.ReadDate(arguments, "date");
        var time = WorkforceAdminTools.ReadTime(arguments, "time");

        if (date is null || time is null)
        {
            return "Give the punch's day as yyyy-MM-dd and its time as HH:mm, in the worker's time zone.";
        }

        var reason = WorkforceAdminTools.Read(arguments, "reason");

        if (reason is null)
        {
            return "Say why the punch is added or changed; the reason is kept.";
        }

        var timeClock = services.GetRequiredService<ITimeClockService>();
        var zone = await TimeClockAdminTools.ZoneOfAsync(services, timesheet, cancellationToken);
        var utc = CalendarTime.ToUtc(date.Value.ToDateTime(time.Value), zone);
        var punchId = WorkforceAdminTools.Read(arguments, "punchId");
        var actor = user.Identity?.Name;
        PlanningResult<TimePunch> result;

        if (punchId is null)
        {
            result = await timeClock.PunchAsync(new TimePunchRequest
            {
                WorkerId = timesheet.WorkerId,
                Kind = kind,
                Utc = utc,
                Source = TimePunchSource.Manager,
                Reason = reason,
            }, actor, cancellationToken);
        }
        else
        {
            var punch = await timeClock.FindAsync(punchId, cancellationToken);

            if (punch is null || punch.WorkerId != timesheet.WorkerId)
            {
                return $"The timesheet's worker has no punch with the ID '{punchId}'.";
            }

            result = await timeClock.CorrectAsync(punchId, kind, utc, reason, actor, cancellationToken);
        }

        if (!result.Succeeded)
        {
            return "The punch wasn't saved: " + string.Join(" ", result.Errors);
        }

        return WorkforceAdminTools.Json(new
        {
            action = punchId is null ? "Added" : "Corrected",
            punchId = result.Value?.ItemId,
            replacesPunchId = punchId,
            timesheetId = timesheet.ItemId,
            kind = kind.ToString(),
            time = WorkforceAdminTools.InZone(utc, zone),
            timeZoneId = zone,
        });
    }
}
