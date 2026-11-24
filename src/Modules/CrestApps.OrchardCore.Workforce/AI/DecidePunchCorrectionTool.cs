using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Approves or declines a worker's request to add or correct a punch, as the timesheet page's corrections do.
/// </summary>
internal sealed class DecidePunchCorrectionTool : WorkforceAdminToolBase
{
    public const string ToolName = "decidePunchCorrection";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "correctionId": { "type": "string", "description": "The correction's ID, from getTimesheet's pendingCorrections." },
        "decision": { "type": "string", "enum": ["Approve", "Decline"], "description": "Approve adds the punch to the worker's time; Decline turns the request down." }
      },
      "required": ["correctionId", "decision"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Approves or declines a worker's request to add or correct a punch. Approving applies it to the timesheet.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ApproveTimesheets;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var decision = WorkforceAdminTools.Read(arguments, "decision");
        var approve = string.Equals(decision, "Approve", StringComparison.OrdinalIgnoreCase);

        if (!approve && !string.Equals(decision, "Decline", StringComparison.OrdinalIgnoreCase))
        {
            return "The decision must be Approve or Decline.";
        }

        var correctionId = WorkforceAdminTools.Read(arguments, "correctionId");
        var scope = await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(user, cancellationToken);
        var attendance = services.GetRequiredService<IAttendanceService>();

        // Only a waiting correction of a team the user manages, as the timesheet page offers them.
        var pending = await attendance.ListPendingCorrectionsAsync(scope.All ? null : scope.TeamIds.ToArray(), null, cancellationToken);
        var correction = correctionId is null ? null : pending.FirstOrDefault(item => item.ItemId == correctionId);

        if (correction is null || !scope.Covers(correction.TeamId))
        {
            return $"No waiting punch correction with the ID '{correctionId}' is in the teams the user manages.";
        }

        var result = await attendance.DecideCorrectionAsync(correction.ItemId, approve, user.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            return "The correction wasn't decided: " + string.Join(" ", result.Errors);
        }

        return WorkforceAdminTools.Json(new
        {
            correctionId = correction.ItemId,
            decision = approve ? "Approve" : "Decline",
            status = result.Value?.Status.ToString(),
            punchId = result.Value?.PunchId,
        });
    }
}
