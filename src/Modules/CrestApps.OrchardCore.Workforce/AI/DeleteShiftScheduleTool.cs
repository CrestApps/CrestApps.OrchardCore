using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Deletes a schedule that was never published, with its draft shifts, as the Shift Schedules page's Delete does.
/// </summary>
internal sealed class DeleteShiftScheduleTool : WorkforceAdminToolBase
{
    public const string ToolName = "deleteShiftSchedule";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "scheduleId": { "type": "string", "description": "The schedule to delete, from listShiftSchedules." }
      },
      "required": ["scheduleId"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Deletes a draft schedule and its draft shifts. A schedule that was ever published can't be deleted; remove its shifts and publish again instead.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageSchedules;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (period, team, error) = await ShiftScheduleToolSupport.FindPeriodAsync(services, user, WorkforceAdminTools.Read(arguments, "scheduleId"), cancellationToken);

        if (error is not null)
        {
            return error;
        }

        if (period.PublishedVersion > 0)
        {
            return "A published schedule can't be deleted; remove its shifts and publish again instead.";
        }

        if (!await services.GetRequiredService<ISchedulePlanningService>().DeletePeriodAsync(period.ItemId, cancellationToken))
        {
            return "The schedule was not deleted.";
        }

        return WorkforceAdminTools.Json(new
        {
            deleted = true,
            id = period.ItemId,
            name = period.Name,
            team = team.Name,
            startDate = WorkforceAdminTools.Day(period.StartDate),
            endDate = WorkforceAdminTools.Day(period.EndDate),
        });
    }
}
