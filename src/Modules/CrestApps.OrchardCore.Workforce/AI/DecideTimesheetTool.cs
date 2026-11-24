using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Approves, sends back or reopens timesheets, as the timesheet page's Decide form and the list's bulk Approve do.
/// </summary>
internal sealed class DecideTimesheetTool : WorkforceAdminToolBase
{
    public const string ToolName = "decideTimesheet";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "timesheetIds": { "type": "array", "items": { "type": "string" }, "minItems": 1, "maxItems": 20, "description": "The timesheets' IDs, from listTimesheets. Up to 20." },
        "decision": { "type": "string", "enum": ["Approve", "Reject", "Reopen"], "description": "Approve the timesheets; Reject sends them back to the worker with the reason; Reopen opens an approved timesheet again. A locked timesheet can't be reopened." },
        "reason": { "type": "string", "description": "For Reject: what the worker must fix." }
      },
      "required": ["timesheetIds", "decision"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Approves one or more timesheets, sends one back to the worker with a reason, or reopens an approved one. The worker is notified.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ApproveTimesheets;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var ids = WorkforceAdminTools.ReadList(arguments, "timesheetIds");
        var decision = WorkforceAdminTools.Read(arguments, "decision");
        var reason = WorkforceAdminTools.Read(arguments, "reason");

        if (ids.Count == 0)
        {
            return "Give the IDs of the timesheets to decide, from listTimesheets.";
        }

        if (ids.Count > WorkforceAdminTools.MaxBulk)
        {
            return $"Decide at most {WorkforceAdminTools.MaxBulk} timesheets at a time.";
        }

        var approve = string.Equals(decision, "Approve", StringComparison.OrdinalIgnoreCase);
        var reject = string.Equals(decision, "Reject", StringComparison.OrdinalIgnoreCase);
        var reopen = string.Equals(decision, "Reopen", StringComparison.OrdinalIgnoreCase);

        if (!approve && !reject && !reopen)
        {
            return "The decision must be Approve, Reject or Reopen.";
        }

        if (reject && reason is null)
        {
            return "Say what the worker must fix: a timesheet is sent back with a reason.";
        }

        var timesheets = services.GetRequiredService<ITimesheetService>();
        var actor = user.Identity?.Name;
        var results = new List<object>();
        var decided = 0;

        foreach (var id in ids)
        {
            var (timesheet, error) = await TimeClockAdminTools.FindTimesheetAsync(services, user, id, cancellationToken);

            if (error is not null)
            {
                results.Add(new { id, changed = false, error });

                continue;
            }

            var previous = timesheet.Status.ToString();
            var result = reject
                ? await timesheets.RejectAsync(id, reason, actor, cancellationToken)
                : reopen
                    ? await timesheets.ReopenAsync(id, actor, cancellationToken)
                    : await timesheets.ApproveAsync(id, actor, cancellationToken);

            if (result.Succeeded)
            {
                decided++;
                results.Add(new { id, changed = true, previousStatus = previous, status = result.Value?.Status.ToString() });
            }
            else
            {
                results.Add(new { id, changed = false, error = string.Join(" ", result.Errors) });
            }
        }

        return WorkforceAdminTools.Json(new
        {
            decision = approve ? "Approve" : reject ? "Reject" : "Reopen",
            decided,
            results,
        });
    }
}
