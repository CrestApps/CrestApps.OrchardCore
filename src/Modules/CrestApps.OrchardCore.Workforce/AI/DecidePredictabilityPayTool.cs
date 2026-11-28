using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Approves or waives predictability pay entries of the teams the user manages, as the Predictability Pay page's
/// Approve and Waive buttons do.
/// </summary>
internal sealed class DecidePredictabilityPayTool : WorkforceAdminToolBase
{
    public const string ToolName = "decidePredictabilityPay";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "entryIds": { "type": "array", "items": { "type": "string" }, "minItems": 1, "maxItems": 20, "description": "The entries' IDs, from listPredictabilityPay. Up to 20." },
        "decision": { "type": "string", "enum": ["Approve", "Waive"], "description": "Approve so the hours are paid with the worker's timesheet, or Waive when they aren't owed." },
        "reason": { "type": "string", "description": "Why the pay isn't owed. Needed to waive." }
      },
      "required": ["entryIds", "decision"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Approves a predictability pay entry so it is paid with the worker's timesheet, or waives it with a reason. A decided entry can't be decided again. Takes several entries at once.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ApprovePredictabilityPay;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var ids = WorkforceAdminTools.ReadList(arguments, "entryIds");
        var decision = WorkforceAdminTools.Read(arguments, "decision");

        if (ids.Count == 0)
        {
            return "Give the IDs of the entries to decide, from listPredictabilityPay.";
        }

        if (ids.Count > WorkforceAdminTools.MaxBulk)
        {
            return $"Decide at most {WorkforceAdminTools.MaxBulk} entries at a time.";
        }

        var approve = string.Equals(decision, "Approve", StringComparison.OrdinalIgnoreCase);

        if (!approve && !string.Equals(decision, "Waive", StringComparison.OrdinalIgnoreCase))
        {
            return "The decision must be Approve or Waive.";
        }

        var reason = WorkforceAdminTools.Read(arguments, "reason");

        if (!approve && reason is null)
        {
            return "Give the reason the pay isn't owed (reason) to waive it.";
        }

        var payService = services.GetRequiredService<IPredictabilityPayService>();
        var scope = await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(user, cancellationToken);
        var actor = user.Identity?.Name;
        var results = new List<object>();
        var decided = 0;

        foreach (var id in ids)
        {
            var entry = await payService.FindAsync(id, cancellationToken);

            if (entry is null)
            {
                results.Add(new { id, changed = false, error = "No predictability pay entry has this ID." });

                continue;
            }

            if (!scope.Covers(entry.TeamId))
            {
                results.Add(new { id, changed = false, error = "The user doesn't manage this worker's team." });

                continue;
            }

            var result = approve
                ? await payService.ApproveAsync(id, actor, cancellationToken)
                : await payService.WaiveAsync(id, reason, actor, cancellationToken);

            if (result.Succeeded)
            {
                decided++;
                results.Add(new { id, changed = true, status = result.Value?.Status.ToString(), hours = result.Value?.Hours });
            }
            else
            {
                results.Add(new { id, changed = false, error = string.Join(" ", result.Errors) });
            }
        }

        return WorkforceAdminTools.Json(new
        {
            decision = approve ? nameof(PredictabilityPayStatus.Approved) : nameof(PredictabilityPayStatus.Waived),
            decided,
            results,
        });
    }
}
