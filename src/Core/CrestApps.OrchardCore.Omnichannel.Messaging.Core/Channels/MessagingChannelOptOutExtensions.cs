using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;

/// <summary>
/// Asks a channel whether a conversation's contact, or its address, has opted out.
/// </summary>
internal static class MessagingChannelOptOutExtensions
{
    /// <summary>
    /// Determines whether the conversation's contact, or the address it is held with, has opted out of the channel.
    /// </summary>
    /// <param name="channel">The conversation's channel.</param>
    /// <param name="conversation">The conversation.</param>
    /// <param name="contentManager">The content manager the contact is loaded with.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static async Task<bool> IsConversationOptedOutAsync(
        this IMessagingChannel channel,
        MessagingConversation conversation,
        IContentManager contentManager,
        CancellationToken cancellationToken = default)
    {
        var contact = string.IsNullOrEmpty(conversation.ContactContentItemId)
            ? null
            : await contentManager.GetAsync(conversation.ContactContentItemId, VersionOptions.Latest);

        return await channel.IsOptedOutAsync(contact, conversation.ContactAddress, cancellationToken);
    }
}
