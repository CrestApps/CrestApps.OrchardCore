using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Claims an open shift for the user, or withdraws the user's claim, as the My Work Shifts page's Claim and Withdraw
/// buttons do. It only ever acts for the worker linked to the user's own account.
/// </summary>
internal sealed class ClaimOpenShiftTool : WorkforceSelfServiceToolBase
{
    public const string ToolName = "claimOpenShift";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "openShiftId": { "type": "string", "description": "The open shift's ID, from listMyOpenShifts." },
        "action": { "type": "string", "enum": ["Claim", "Withdraw"], "description": "Claim the shift, or Withdraw the user's own claim. Claim when left out." }
      },
      "required": ["openShiftId"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Claims an open shift for the user, or withdraws their claim. With first come, first served the shift is theirs at once and goes on their schedule; otherwise the claim waits for a manager. Confirm the shift with the user first.";

    public override JsonElement JsonSchema => _schema;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, StaffMember worker, CancellationToken cancellationToken)
    {
        var id = WorkforceAdminTools.Read(arguments, "openShiftId");

        if (id is null)
        {
            return "Give the open shift's ID (openShiftId), from listMyOpenShifts.";
        }

        var action = WorkforceAdminTools.Read(arguments, "action") ?? "Claim";
        var withdraw = string.Equals(action, "Withdraw", StringComparison.OrdinalIgnoreCase);

        if (!withdraw && !string.Equals(action, "Claim", StringComparison.OrdinalIgnoreCase))
        {
            return "The action must be Claim or Withdraw.";
        }

        var openShifts = services.GetRequiredService<IOpenShiftService>();
        var result = withdraw
            ? await openShifts.WithdrawClaimAsync(id, worker.ItemId, user.Identity?.Name, cancellationToken)
            : await openShifts.ClaimAsync(id, worker.ItemId, user.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            return $"Nothing changed: {string.Join(" ", result.Errors)}";
        }

        var shift = result.Value ?? await openShifts.FindAsync(id, cancellationToken);
        var outcome = withdraw
            ? "Your claim has been withdrawn."
            : shift.FilledWorkerId == worker.ItemId
                ? "The shift is yours; it is on your schedule."
                : "Your claim has been sent. You'll hear when a manager decides.";

        return WorkforceAdminTools.Json(new
        {
            action = withdraw ? "Withdraw" : "Claim",
            outcome,
            filled = !withdraw && shift.FilledWorkerId == worker.ItemId,
            shift = await OpenShiftToolSupport.DescribeShiftAsync(services, shift, withClaims: false, cancellationToken),
        });
    }
}
