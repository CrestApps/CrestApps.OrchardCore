using System.Security.Claims;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.AI;

/// <summary>
/// Finds the conversation a messaging tool works on, by its ID or by the customer's address on a channel as the
/// workspace's "message this customer" link does, and checks that the caller may perform the operation on it.
/// </summary>
internal static class MessagingConversationLookup
{
    /// <summary>
    /// The schema properties that name a conversation.
    /// </summary>
    public const string SchemaProperties =
    """
        "conversationId": {
          "type": "string",
          "description": "The ID of the conversation. Call listMessagingConversations to find it."
        },
        "channel": {
          "type": "string",
          "description": "With contactAddress, instead of conversationId: the channel of the conversation, such as SMS."
        },
        "contactAddress": {
          "type": "string",
          "description": "With channel, instead of conversationId: the customer's address, such as +15550107788."
        }
    """;

    /// <summary>
    /// Finds the conversation and checks the operation on it.
    /// </summary>
    /// <returns>The conversation, or the sentence that says why there is none the caller may use.</returns>
    public static async Task<(MessagingConversation Conversation, string Refusal)> FindAsync(
        AIFunctionArguments arguments,
        IServiceProvider services,
        ClaimsPrincipal user,
        ConversationOperation operation,
        CancellationToken cancellationToken)
    {
        var store = services.GetRequiredService<IMessagingConversationStore>();
        var id = Read(arguments, "conversationId");
        MessagingConversation conversation;

        if (id is not null)
        {
            conversation = await store.FindByIdAsync(id, cancellationToken);
        }
        else
        {
            var channelName = Read(arguments, "channel");
            var address = Read(arguments, "contactAddress");

            if (channelName is null || address is null)
            {
                return (null, "Give the conversationId, or the channel and the contactAddress of the customer.");
            }

            var channel = services.GetRequiredService<IMessagingChannelResolver>().Get(channelName);

            if (channel is null)
            {
                return (null, $"No messaging channel named '{channelName}' is enabled.");
            }

            var normalized = channel.NormalizeAddress(address);

            conversation = string.IsNullOrEmpty(normalized)
                ? null
                : await store.FindByContactAsync(channel.Name, normalized, cancellationToken);
        }

        // A conversation the caller may not open is reported like one that does not exist, as the workspace does not
        // tell an agent which colleague's customers exist.
        if (conversation is null ||
            !await services.GetRequiredService<MessagingWorkspaceBuilder>().AuthorizeAsync(user, conversation, ConversationOperation.View))
        {
            return (null, "No conversation you may open matches. Call listMessagingConversations to find it.");
        }

        if (operation != ConversationOperation.View &&
            !await services.GetRequiredService<MessagingWorkspaceBuilder>().AuthorizeAsync(user, conversation, operation))
        {
            return (null, $"You are not allowed to {Describe(operation)} this conversation.");
        }

        return (conversation, null);
    }

    private static string Read(AIFunctionArguments arguments, string key)
        => arguments.TryGetFirstString(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static string Describe(ConversationOperation operation)
        => operation switch
        {
            ConversationOperation.Send => "send on",
            ConversationOperation.Claim => "claim",
            ConversationOperation.Close => "close",
            ConversationOperation.Snooze => "snooze",
            ConversationOperation.Transfer => "transfer",
            _ => operation.ToString().ToLowerInvariant(),
        };
}
