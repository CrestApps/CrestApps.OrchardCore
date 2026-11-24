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
/// Changes a conversation's handling state with the services and conversation checks of the workspace's claim, status,
/// read and favorite actions.
/// </summary>
internal sealed class UpdateMessagingConversationTool : MessagingAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "updateMessagingConversation";

    private static readonly JsonElement _jsonSchema = Schema(
    $$"""
    {
      "type": "object",
      "properties": {
        {{MessagingConversationLookup.SchemaProperties}},
        "action": {
          "type": "string",
          "enum": ["claim", "open", "snooze", "close", "markRead", "favorite", "unfavorite"],
          "description": "claim takes the conversation; open, snooze and close set its status; markRead marks it read for everyone; favorite and unfavorite star or unstar the customer for the caller."
        }
      },
      "required": ["action"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Changes a conversation's handling state: claim it, set status open/snoozed/closed, mark it read, or set/clear it as a favorite.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var action = ReadString(arguments, "action");
        (ConversationOperation? Operation, ConversationStatus? Status) choice = action?.ToLowerInvariant() switch
        {
            "claim" => (ConversationOperation.Claim, null),
            "open" => (ConversationOperation.View, ConversationStatus.Open),
            "snooze" => (ConversationOperation.Snooze, ConversationStatus.Snoozed),
            "close" => (ConversationOperation.Close, ConversationStatus.Closed),
            "markread" or "favorite" or "unfavorite" => (ConversationOperation.View, null),
            _ => (null, null),
        };
        var (operation, status) = choice;

        if (operation is null)
        {
            return "Nothing was changed: action must be claim, open, snooze, close, markRead, favorite or unfavorite.";
        }

        var (conversation, refusal) = await MessagingConversationLookup.FindAsync(arguments, services, user, operation.Value, cancellationToken);

        if (refusal is not null)
        {
            return "Nothing was changed: " + refusal;
        }

        var workspaceBuilder = services.GetRequiredService<MessagingWorkspaceBuilder>();
        var conversationService = services.GetRequiredService<IMessagingConversationService>();

        switch (action.ToLowerInvariant())
        {
            case "claim":
            {
                var agent = await workspaceBuilder.GetCurrentAgentAsync(user);

                if (agent is null)
                {
                    return "Nothing was changed: you must have an agent profile to claim conversations.";
                }

                var result = await conversationService.ClaimAsync(conversation.ItemId, agent.ItemId, user, cancellationToken);

                return result.Succeeded
                    ? Done(conversation.ItemId, "claimed")
                    : $"The conversation could not be claimed: {result.Error}";
            }

            case "markread":
                if (MessagingConversationReadState.MarkRead(conversation))
                {
                    await workspaceBuilder.SaveReadAsync(user, conversation);
                }

                return Done(conversation.ItemId, "marked read");

            case "favorite":
            case "unfavorite":
            {
                var agent = await workspaceBuilder.GetCurrentAgentAsync(user);

                if (agent is null)
                {
                    return "Nothing was changed: you must have an agent profile to keep favorites.";
                }

                var favorite = action.Equals("favorite", StringComparison.OrdinalIgnoreCase);

                await services.GetRequiredService<IMessagingFavoritesService>()
                    .SetFavoriteAsync(agent, conversation, conversation.ContactAddress, favorite, cancellationToken);

                return Done(conversation.ItemId, favorite ? "starred" : "unstarred");
            }

            default:
            {
                var result = await conversationService.SetStatusAsync(conversation.ItemId, status.Value, user, cancellationToken);

                return result.Succeeded
                    ? Done(conversation.ItemId, $"set to {status.Value}")
                    : $"The conversation could not be updated: {result.Error}";
            }
        }
    }

    private static string Done(string conversationId, string change)
        => Json(new
        {
            conversationId,
            change,
        });
}
