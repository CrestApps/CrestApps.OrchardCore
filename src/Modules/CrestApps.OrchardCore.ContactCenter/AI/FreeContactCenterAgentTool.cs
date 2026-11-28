using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Releases an agent from work that has already ended, as the live dashboard's Free agent action does. The intervention
/// service authorizes it against the queues the caller supervises and releases only work it finds stale.
/// </summary>
internal sealed class FreeContactCenterAgentTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "freeContactCenterAgent";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "agentId": {
          "type": "string",
          "description": "The agent, by agent id or user name."
        }
      },
      "required": ["agentId"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Releases an agent who is stuck on work that has already ended so they can receive work again. Use it only when the agent's state shows stale work. Only agents of the queues the caller supervises.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ContactCenterPermissions.InterveneInCalls;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var supervisorId = ContactCenterOperationsSupport.GetUserId(user);

        if (supervisorId is null || !await IsAuthorizedAsync(services, user, ContactCenterPermissions.MonitorContactCenter))
        {
            return NotAllowedMessage;
        }

        var agentText = new ContactCenterToolArguments(arguments).GetString("agentId");

        if (agentText is null)
        {
            return "Nothing was changed: give the agent.";
        }

        var agent = await LiveCallTargets.FindAgentAsync(services, agentText, cancellationToken);

        if (agent is null)
        {
            return $"Nothing was changed: no agent has the id or user name '{agentText}'.";
        }

        var result = await services.GetRequiredService<IContactCenterSupervisorInterventionService>()
            .FreeAgentAsync(agent.ItemId, supervisorId, user, cancellationToken);

        return ContactCenterOperationsSupport.Outcome(result, $"{SetContactCenterAgentStateTool.Describe(agent)} was released from the ended work.");
    }
}
