using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Removes a punch from a timesheet's worker, with a reason, as the timesheet page's Remove punch form does. The
/// removal is recorded as a punch that cancels the original, which stays in the history.
/// </summary>
internal sealed class RemoveTimesheetPunchTool : TimesheetPunchToolBase
{
    public const string ToolName = "removeTimesheetPunch";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "timesheetId": { "type": "string", "description": "The timesheet's ID, from listTimesheets or getTimesheet." },
        "punchId": { "type": "string", "description": "The punch's ID, from getTimesheet." },
        "reason": { "type": "string", "description": "Why the punch is removed; kept with the removal." }
      },
      "required": ["timesheetId", "punchId", "reason"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Removes a punch from a worker's timesheet, with a reason that is kept. A punch removed by mistake can be added again with saveTimesheetPunch.";

    public override JsonElement JsonSchema => _schema;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (timesheet, error) = await TimeClockAdminTools.FindTimesheetAsync(services, user, WorkforceAdminTools.Read(arguments, "timesheetId"), cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var punchId = WorkforceAdminTools.Read(arguments, "punchId");
        var reason = WorkforceAdminTools.Read(arguments, "reason");

        if (reason is null)
        {
            return "Say why the punch is removed; the reason is kept.";
        }

        var timeClock = services.GetRequiredService<ITimeClockService>();
        var punch = punchId is null ? null : await timeClock.FindAsync(punchId, cancellationToken);

        if (punch is null || punch.WorkerId != timesheet.WorkerId)
        {
            return $"The timesheet's worker has no punch with the ID '{punchId}'.";
        }

        var zone = await TimeClockAdminTools.ZoneOfAsync(services, timesheet, cancellationToken);
        var result = await timeClock.RemoveAsync(punchId, reason, user.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            return "The punch wasn't removed: " + string.Join(" ", result.Errors);
        }

        return WorkforceAdminTools.Json(new
        {
            removedPunchId = punchId,
            timesheetId = timesheet.ItemId,
            kind = punch.Kind.ToString(),
            time = WorkforceAdminTools.InZone(punch.Utc, zone),
            timeZoneId = zone,
        });
    }
}
