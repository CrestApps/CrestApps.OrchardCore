using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Approves, declines or cancels time-off requests of the teams the user manages, as the request page's buttons and the
/// queue's bulk actions do.
/// </summary>
internal sealed class DecideTimeOffRequestTool : WorkforceAdminToolBase
{
    public const string ToolName = "decideTimeOffRequest";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "requestIds": { "type": "array", "items": { "type": "string" }, "minItems": 1, "maxItems": 20, "description": "The requests' IDs, from listTimeOffRequests. Up to 20." },
        "decision": { "type": "string", "enum": ["Approve", "Decline", "Cancel"], "description": "Approve or Decline a waiting request; Cancel withdraws a waiting request or cancels approved time off and gives the balance back." },
        "note": { "type": "string", "description": "Optional. A note to the worker: why, for a decline or a cancel." }
      },
      "required": ["requestIds", "decision"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Approves, declines or cancels a time-off request, with a note. Approving books the days, puts them on the team calendar and handles the worker's shifts by the team's rule; the worker is notified. Takes several requests at once.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ApproveTimeOff;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var ids = WorkforceAdminTools.ReadList(arguments, "requestIds");
        var decision = WorkforceAdminTools.Read(arguments, "decision");

        if (ids.Count == 0)
        {
            return "Give the IDs of the requests to decide, from listTimeOffRequests.";
        }

        if (ids.Count > WorkforceAdminTools.MaxBulk)
        {
            return $"Decide at most {WorkforceAdminTools.MaxBulk} requests at a time.";
        }

        if (decision is null || !(decision.Equals("Approve", StringComparison.OrdinalIgnoreCase) || decision.Equals("Decline", StringComparison.OrdinalIgnoreCase) || decision.Equals("Cancel", StringComparison.OrdinalIgnoreCase)))
        {
            return "The decision must be Approve, Decline or Cancel.";
        }

        var note = WorkforceAdminTools.Read(arguments, "note");
        var requests = services.GetRequiredService<ITimeOffRequestService>();
        var scope = await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(user, cancellationToken);
        var actor = user.Identity?.Name;
        var results = new List<object>();
        var changed = 0;

        foreach (var id in ids)
        {
            var request = await requests.FindAsync(id, cancellationToken);

            if (request is null)
            {
                results.Add(new { id, changed = false, error = "No time-off request has this ID." });

                continue;
            }

            if (!scope.Covers(request.TeamId))
            {
                results.Add(new { id, changed = false, error = "The user doesn't manage this worker's team." });

                continue;
            }

            var previous = request.Status.ToString();
            var result = decision.Equals("Approve", StringComparison.OrdinalIgnoreCase)
                ? await requests.ApproveAsync(id, actor, note, cancellationToken)
                : decision.Equals("Decline", StringComparison.OrdinalIgnoreCase)
                    ? await requests.DeclineAsync(id, actor, note, cancellationToken)
                    : await requests.CancelAsync(id, actor, note, cancellationToken);

            if (result.Succeeded)
            {
                changed++;
                results.Add(new { id, changed = true, previousStatus = previous, status = result.Value?.Status.ToString() });
            }
            else
            {
                results.Add(new { id, changed = false, error = string.Join(" ", result.Errors) });
            }
        }

        return WorkforceAdminTools.Json(new
        {
            decision,
            decided = changed,
            results,
        });
    }
}
