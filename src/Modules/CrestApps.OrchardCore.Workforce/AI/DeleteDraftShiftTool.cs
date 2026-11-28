using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Removes a shift from a draft schedule, as the planner board's Delete does.
/// </summary>
internal sealed class DeleteDraftShiftTool : WorkforceAdminToolBase
{
    public const string ToolName = "deleteDraftShift";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "scheduleId": { "type": "string", "description": "The schedule's ID, from listShiftSchedules." },
        "shiftId": { "type": "string", "description": "The draft shift's ID, from getShiftSchedule." }
      },
      "required": ["scheduleId", "shiftId"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Removes a shift from a draft schedule. A shift already on the published schedule stays there until the schedule is published again; to take it off now, use changePublishedShift.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageSchedules;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (period, _, error) = await ShiftScheduleToolSupport.FindPeriodAsync(services, user, WorkforceAdminTools.Read(arguments, "scheduleId"), cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var shiftId = WorkforceAdminTools.Read(arguments, "shiftId");
        var shift = await ShiftScheduleToolSupport.FindDraftAsync(services, period, shiftId, cancellationToken);

        if (shift is null)
        {
            return $"The schedule has no draft shift with the ID '{shiftId}'. Use getShiftSchedule to find it.";
        }

        if (!await services.GetRequiredService<ISchedulePlanningService>().DeleteDraftShiftAsync(shift.ItemId, user.Identity?.Name, cancellationToken))
        {
            return "The shift was not deleted: the schedule may be locked.";
        }

        return WorkforceAdminTools.Json(new
        {
            deleted = true,
            scheduleId = period.ItemId,
            shiftId = shift.ItemId,
            date = WorkforceAdminTools.Day(shift.Date),
            note = period.PublishedVersion > 0 ? "The schedule is already published: if this shift was on it, it stays there until the schedule is published again." : null,
        });
    }
}
