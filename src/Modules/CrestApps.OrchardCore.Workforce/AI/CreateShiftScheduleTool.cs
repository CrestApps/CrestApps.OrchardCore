using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Starts a draft schedule for a team, as the Shift Schedules page's Create form does.
/// </summary>
internal sealed class CreateShiftScheduleTool : WorkforceAdminToolBase
{
    public const string ToolName = "createShiftSchedule";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "teamName": { "type": "string", "description": "The team the user manages, by name or ID." },
        "startDate": { "type": "string", "description": "The schedule's first day (yyyy-MM-dd), usually a Monday." },
        "weeks": { "type": "integer", "minimum": 1, "maximum": 12, "description": "How many weeks the schedule lasts; 2 by default, as on the page." },
        "name": { "type": "string", "description": "Optional. The schedule's name; a name from the dates is used when it is left out." }
      },
      "required": ["teamName", "startDate"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Starts a draft schedule for a team from a start date for a number of weeks. Nothing is visible to workers until it is published. Fill it with saveDraftShift or fillShiftSchedule.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageSchedules;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (team, teamError) = await ShiftScheduleToolSupport.FindTeamAsync(services, user, WorkforceAdminTools.Read(arguments, "teamName"), cancellationToken);

        if (teamError is not null)
        {
            return teamError;
        }

        var startDate = WorkforceAdminTools.ReadDate(arguments, "startDate");

        if (startDate is null)
        {
            return "Give the first day (startDate) as yyyy-MM-dd.";
        }

        var weeks = WorkforceAdminTools.ReadInt(arguments, "weeks") ?? 2;
        var result = await services.GetRequiredService<ISchedulePlanningService>().CreatePeriodAsync(team.ItemId, startDate.Value, weeks, WorkforceAdminTools.Read(arguments, "name"), user.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            return "The schedule was not created: " + string.Join(" ", result.Errors);
        }

        var period = result.Value;

        return WorkforceAdminTools.Json(new
        {
            created = true,
            id = period.ItemId,
            name = period.Name,
            team = team.Name,
            startDate = WorkforceAdminTools.Day(period.StartDate),
            endDate = WorkforceAdminTools.Day(period.EndDate),
            status = period.Status.ToString(),
            link = ShiftScheduleToolSupport.PlanPath(services, period.ItemId),
        });
    }
}
