using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Gives an open shift to a worker or declines a worker's claim, as the open shift page's Assign and Decline buttons do.
/// </summary>
internal sealed class DecideOpenShiftClaimTool : WorkforceAdminToolBase
{
    public const string ToolName = "decideOpenShiftClaim";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "openShiftId": { "type": "string", "description": "The open shift's ID, from listOpenShifts." },
        "decision": { "type": "string", "enum": ["Assign", "Decline"], "description": "Assign gives the shift to the worker (one who claimed it, or any eligible worker) and puts it on their schedule; Decline turns down the worker's claim." },
        "workerId": { "type": "string", "description": "The worker's ID. Give this or workerName." },
        "workerName": { "type": "string", "description": "The worker's name, among the workers the user manages. Give this or workerId." },
        "note": { "type": "string", "description": "Optional. For a decline, why; the worker sees it." }
      },
      "required": ["openShiftId", "decision"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Gives an open shift to a worker (one who claimed it or any eligible worker), or declines a worker's claim with a note. The worker is notified.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageOpenShifts;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var decision = WorkforceAdminTools.Read(arguments, "decision");
        var assign = string.Equals(decision, "Assign", StringComparison.OrdinalIgnoreCase);

        if (!assign && !string.Equals(decision, "Decline", StringComparison.OrdinalIgnoreCase))
        {
            return "The decision must be Assign or Decline.";
        }

        var (shift, error) = await OpenShiftToolSupport.FindManagedShiftAsync(services, user, WorkforceAdminTools.Read(arguments, "openShiftId"), cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var (worker, workerError) = await WorkforceAdminTools.FindWorkerAsync(services, user, arguments, cancellationToken);

        if (workerError is not null)
        {
            return workerError;
        }

        var openShifts = services.GetRequiredService<IOpenShiftService>();
        var result = assign
            ? await openShifts.AssignAsync(shift.ItemId, worker.ItemId, user.Identity?.Name, cancellationToken)
            : await openShifts.DeclineClaimAsync(shift.ItemId, worker.ItemId, WorkforceAdminTools.Read(arguments, "note"), user.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            return $"Nothing changed: {string.Join(" ", result.Errors)}";
        }

        return WorkforceAdminTools.Json(new
        {
            decision = assign ? "Assign" : "Decline",
            workerId = worker.ItemId,
            worker = worker.DisplayName,
            openShift = await OpenShiftToolSupport.DescribeShiftAsync(services, result.Value, withClaims: true, cancellationToken),
        });
    }
}
