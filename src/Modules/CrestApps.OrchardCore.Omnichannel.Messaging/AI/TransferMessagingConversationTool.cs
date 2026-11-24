using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.AI;

/// <summary>
/// Hands a conversation to another agent or to a queue with the workspace's transfer service, transfer targets and
/// conversation check. Without a target it lists the people and queues the conversation can go to, as the workspace's
/// transfer picker does.
/// </summary>
internal sealed class TransferMessagingConversationTool : MessagingAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "transferMessagingConversation";

    private static readonly JsonElement _jsonSchema = Schema(
    $$"""
    {
      "type": "object",
      "properties": {
        {{MessagingConversationLookup.SchemaProperties}},
        "targetType": {
          "type": "string",
          "enum": ["agent", "queue"],
          "description": "Whether the conversation goes to a person or back to a queue's shared pool."
        },
        "targetId": {
          "type": "string",
          "description": "The ID of the agent or queue. Leave it out to list the targets the conversation can go to."
        },
        "query": {
          "type": "string",
          "description": "Without targetId: text the listed targets' names contain."
        },
        "note": {
          "type": "string",
          "description": "A note for whoever receives the conversation."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Hands a conversation to another agent or to a queue with an optional note. Call it without targetId to list the transfer targets; an agent who cannot take it is refused.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var targetTypeText = ReadString(arguments, "targetType")?.ToLowerInvariant();
        ConversationRouteTargetType? targetType = targetTypeText switch
        {
            "agent" => ConversationRouteTargetType.Agent,
            "queue" => ConversationRouteTargetType.Queue,
            _ => null,
        };

        if (targetTypeText is not null && targetType is null)
        {
            return "Nothing was transferred: targetType must be agent or queue.";
        }

        var (conversation, refusal) = await MessagingConversationLookup.FindAsync(arguments, services, user, ConversationOperation.Transfer, cancellationToken);

        if (refusal is not null)
        {
            return "Nothing was transferred: " + refusal;
        }

        var targets = services.GetRequiredService<MessagingTransferTargets>();
        var targetId = ReadString(arguments, "targetId");

        if (targetId is null)
        {
            var query = ReadString(arguments, "query");

            return Json(new
            {
                message = "Call again with targetType and targetId to transfer the conversation.",
                agents = targetType is null or ConversationRouteTargetType.Agent
                    ? (await targets.SearchAgentsAsync(conversation, query, cancellationToken)).Select(target => new { id = target.Value, name = target.Text })
                    : null,
                queues = (targetType is null or ConversationRouteTargetType.Queue) && targets.SupportsQueues
                    ? (await targets.SearchQueuesAsync(conversation, query, cancellationToken)).Select(target => new { id = target.Value, name = target.Text })
                    : null,
            });
        }

        if (targetType is null)
        {
            return "Nothing was transferred: say with targetType whether targetId is an agent or a queue.";
        }

        // The workspace's picker only offers people who can work messaging conversations; the tool is held to the same
        // rule, since a conversation sent to someone who cannot open the workspace would be stranded.
        if (targetType == ConversationRouteTargetType.Agent && !await targets.IsEligibleAgentAsync(targetId, cancellationToken))
        {
            return "Nothing was transferred: that person cannot work messaging conversations. Call without targetId to list who can.";
        }

        var agent = await services.GetRequiredService<MessagingWorkspaceBuilder>().GetCurrentAgentAsync(user);
        var result = await services.GetRequiredService<IMessagingConversationTransferService>().TransferAsync(new MessagingTransferRequest
        {
            ConversationId = conversation.ItemId,
            TargetType = targetType.Value,
            TargetId = targetId,
            Note = ReadString(arguments, "note"),
            ActingAgentId = agent?.ItemId,
            Principal = user,
        }, cancellationToken);

        if (!result.Succeeded)
        {
            return $"The conversation could not be transferred: {result.Error}";
        }

        return Json(new
        {
            transferred = true,
            conversationId = conversation.ItemId,
            to = result.Event?.ToName,
            targetType = targetType.Value.ToString(),
            targetId,
        });
    }
}
