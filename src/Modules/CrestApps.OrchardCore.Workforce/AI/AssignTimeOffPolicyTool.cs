using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Assigns a time-off policy to one worker, or takes it away, as the Assign and Unassign forms of the worker's
/// time-off page do.
/// </summary>
internal sealed class AssignTimeOffPolicyTool : WorkforceAdminToolBase
{
    public const string ToolName = "assignTimeOffPolicy";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "workerId": { "type": "string", "description": "The worker's ID. Give this or workerName." },
        "workerName": { "type": "string", "description": "The worker's name. Give this or workerId." },
        "policy": { "type": "string", "description": "The time-off policy, by name or ID." },
        "action": { "type": "string", "enum": ["Assign", "Unassign"], "description": "Assign gives the worker the policy alone (it takes precedence over their teams' policies of its type); Unassign removes every assignment of the policy to the worker alone. Policies a worker gets from a team are changed on the policy, not here." },
        "effectiveFrom": { "type": "string", "description": "For Assign, optional: the first day the policy applies (yyyy-MM-dd)." },
        "effectiveTo": { "type": "string", "description": "For Assign, optional: the last day the policy applies (yyyy-MM-dd)." }
      },
      "required": ["policy", "action"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Assigns a time-off policy to a worker from a date, or takes it away. A worker without a policy for a type cannot ask for that type.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageTimeOffPolicies;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var action = WorkforceAdminTools.Read(arguments, "action");
        var assign = string.Equals(action, "Assign", StringComparison.OrdinalIgnoreCase);

        if (!assign && !string.Equals(action, "Unassign", StringComparison.OrdinalIgnoreCase))
        {
            return "The action must be Assign or Unassign.";
        }

        // The admin page lets anyone with the permission assign policies, whichever team the worker is in.
        var (worker, error) = await WorkforceAdminTools.FindWorkerAsync(services, user, arguments, requireScope: false, cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var policyManager = services.GetRequiredService<ITimeOffPolicyManager>();
        var policyText = WorkforceAdminTools.Read(arguments, "policy");
        var policies = (await policyManager.GetAllAsync(cancellationToken)).ToList();
        var found = policyText is null
            ? null
            : policies.FirstOrDefault(item => item.ItemId == policyText) ?? policies.FirstOrDefault(item => WorkforceNames.Normalize(item.Name) == WorkforceNames.Normalize(policyText));

        if (found is null)
        {
            return $"No time-off policy is called '{policyText}'. The policies are: {string.Join(", ", policies.Select(item => item.Name))}.";
        }

        // Read by its ID, as the admin page reads the policy it changes.
        var policy = await policyManager.FindByIdAsync(found.ItemId, cancellationToken);

        if (assign)
        {
            var from = WorkforceAdminTools.ReadDate(arguments, "effectiveFrom");
            var to = WorkforceAdminTools.ReadDate(arguments, "effectiveTo");

            if (from.HasValue && to.HasValue && to.Value < from.Value)
            {
                return "The last day can't be before the first day.";
            }

            var validation = await TimeOffPolicyAssignments.AssignAsync(policyManager, policy, worker.ItemId, from, to, cancellationToken);

            if (!validation.Succeeded)
            {
                return "The policy wasn't assigned: " + string.Join(" ", validation.Errors.Select(item => item.ErrorMessage));
            }

            return WorkforceAdminTools.Json(new
            {
                action = "Assign",
                worker = new { id = worker.ItemId, name = worker.DisplayName },
                policy = new { id = policy.ItemId, name = policy.Name },
                effectiveFrom = WorkforceAdminTools.Day(from),
                effectiveTo = WorkforceAdminTools.Day(to),
            });
        }

        var (removed, unassigned) = await TimeOffPolicyAssignments.UnassignAsync(policyManager, policy, worker.ItemId, cancellationToken);

        if (!unassigned.Succeeded)
        {
            return "The policy wasn't taken away: " + string.Join(" ", unassigned.Errors.Select(item => item.ErrorMessage));
        }

        return WorkforceAdminTools.Json(new
        {
            action = "Unassign",
            worker = new { id = worker.ItemId, name = worker.DisplayName },
            policy = new { id = policy.ItemId, name = policy.Name },
            removedAssignments = removed,
            note = removed == 0 ? "The policy wasn't assigned to the worker alone; a policy that comes from the worker's team is changed on the policy." : null,
        });
    }
}
