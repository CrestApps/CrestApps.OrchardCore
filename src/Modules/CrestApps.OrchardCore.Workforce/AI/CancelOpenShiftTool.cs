using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Stops offering an open shift, as the open shift page's Cancel button does.
/// </summary>
internal sealed class CancelOpenShiftTool : WorkforceAdminToolBase
{
    public const string ToolName = "cancelOpenShift";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "openShiftId": { "type": "string", "description": "The open shift's ID, from listOpenShifts." }
      },
      "required": ["openShiftId"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Cancels an open shift of a team the user manages so it can no longer be claimed. A filled shift is changed on the schedule instead.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageOpenShifts;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (shift, error) = await OpenShiftToolSupport.FindManagedShiftAsync(services, user, WorkforceAdminTools.Read(arguments, "openShiftId"), cancellationToken);

        if (error is not null)
        {
            return error;
        }

        if (shift.Status is OpenShiftStatus.Filled or OpenShiftStatus.Canceled)
        {
            return $"The open shift can't be canceled: its status is {shift.Status}.";
        }

        if (!await services.GetRequiredService<IOpenShiftService>().CancelAsync(shift.ItemId, user.Identity?.Name, cancellationToken))
        {
            return "The open shift couldn't be canceled; it may have been filled or canceled meanwhile.";
        }

        return WorkforceAdminTools.Json(new
        {
            canceled = true,
            openShiftId = shift.ItemId,
            date = WorkforceAdminTools.Day(shift.Date),
        });
    }
}
