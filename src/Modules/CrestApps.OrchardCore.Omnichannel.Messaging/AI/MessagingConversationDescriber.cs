using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.AI;

/// <summary>
/// Describes a messaging conversation for the messaging tools from an explicit list of fields: never the AI session,
/// the provider's message identifiers or the attachments' addresses.
/// </summary>
internal static class MessagingConversationDescriber
{
    /// <summary>
    /// Describes one conversation as a row of the inbox. The customer's address is included only when one conversation
    /// is read, as the catalog asks: a list row names the contact by ID only.
    /// </summary>
    public static async Task<object> DescribeAsync(IServiceProvider services, MessagingConversation conversation, bool includeContactAddress, CancellationToken cancellationToken)
    {
        var channel = services.GetService<IMessagingChannelResolver>()?.Get(conversation.Channel);
        var localClock = services.GetService<ILocalClock>();
        var holderId = conversation.GetHolderAgentId();
        var agentNames = services.GetService<IMessagingAgentNameProvider>();

        return new
        {
            id = conversation.ItemId,
            channel = conversation.Channel,
            contactAddress = includeContactAddress ? channel?.FormatAddress(conversation.ContactAddress) ?? conversation.ContactAddress : null,
            contactId = conversation.ContactContentItemId,
            ourAddress = channel?.FormatAddress(conversation.ServiceAddress) ?? conversation.ServiceAddress,
            status = conversation.Status.ToString(),
            assignmentStatus = conversation.AssignmentStatus.ToString(),
            ownerType = conversation.OwnerType.ToString(),
            queueId = conversation.OwnerType == ConversationOwnerType.Queue ? conversation.OwnerId : null,
            holderAgentId = holderId,
            holderName = holderId is null || agentNames is null ? null : await agentNames.GetDisplayNameAsync(holderId, cancellationToken),
            campaignId = conversation.CampaignId,
            isRead = conversation.IsRead,
            unreadCount = conversation.UnreadCount,
            lastMessage = await MessagingAIToolBase.ToLocalAsync(localClock, conversation.LastMessageUtc),
            lastMessagePreview = conversation.LastMessagePreview,
            firstResponse = new
            {
                due = await MessagingAIToolBase.ToLocalAsync(localClock, conversation.FirstResponseDueUtc),
                responded = await MessagingAIToolBase.ToLocalAsync(localClock, conversation.FirstRespondedUtc),
                breached = conversation.FirstResponseBreached,
            },
        };
    }
}
