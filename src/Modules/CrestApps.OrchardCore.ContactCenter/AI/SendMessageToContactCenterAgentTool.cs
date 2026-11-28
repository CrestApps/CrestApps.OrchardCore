using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Shows a short message from the caller on an agent's desktop, as the live dashboard's Message action does. The
/// intervention service authorizes it against the queues the caller supervises.
/// </summary>
internal sealed class SendMessageToContactCenterAgentTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "sendMessageToContactCenterAgent";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "agentId": {
          "type": "string",
          "description": "The agent, by agent id or user name."
        },
        "text": {
          "type": "string",
          "description": "The message, shown on the agent's desktop under the caller's name."
        }
      },
      "required": ["agentId", "text"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Shows a short message from the supervisor on an agent's desktop. Only agents of the queues the caller supervises.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ContactCenterPermissions.MonitorContactCenter;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var supervisorId = ContactCenterOperationsSupport.GetUserId(user);

        if (supervisorId is null)
        {
            return NotAllowedMessage;
        }

        var reader = new ContactCenterToolArguments(arguments);
        var agentText = reader.GetString("agentId");
        var text = reader.GetString("text");

        if (agentText is null || text is null)
        {
            return "Nothing was sent: give the agent and the message.";
        }

        var agent = await LiveCallTargets.FindAgentAsync(services, agentText, cancellationToken);

        if (agent is null)
        {
            return $"Nothing was sent: no agent has the id or user name '{agentText}'.";
        }

        var result = await services.GetRequiredService<IContactCenterSupervisorInterventionService>()
            .SendMessageAsync(agent.ItemId, supervisorId, user.Identity?.Name, user, text, cancellationToken);

        return ContactCenterOperationsSupport.Outcome(result, $"The message was sent to {SetContactCenterAgentStateTool.Describe(agent)}.");
    }
}
