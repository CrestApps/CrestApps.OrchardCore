using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

// The lookups that tie outbound messages to their thread.
public sealed partial class MessagingConversationService
{
    /// <summary>
    /// Finds a conversation's newest outbound message in one indexed query, instead of loading the whole outbound
    /// history to pick the first.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="conversationId">The conversation.</param>
    /// <param name="withoutProviderMessageId">Whether only a message that has no provider message id yet is wanted.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    internal static Task<OmnichannelMessage> FindNewestOutboundAsync(
        ISession session,
        string conversationId,
        bool withoutProviderMessageId,
        CancellationToken cancellationToken)
    {
        var query = withoutProviderMessageId
            ? session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
                index => index.ConversationId == conversationId &&
                    !index.IsInbound &&
                    (index.ProviderMessageId == null || index.ProviderMessageId == string.Empty),
                collection: OmnichannelConstants.CollectionName)
            : session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
                index => index.ConversationId == conversationId && !index.IsInbound,
                collection: OmnichannelConstants.CollectionName);

        return query
            .OrderByDescending(index => index.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Links the outbound messages sent to a contact before they had a conversation (bulk mail opens none) to the
    /// conversation that now exists for the same address pair, so the thread shows what the contact is answering. Call it
    /// when a conversation is created, in the same unit of work.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="conversation">The conversation just created.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    internal static async Task AdoptUnthreadedMessagesAsync(
        ISession session,
        MessagingConversation conversation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(conversation);

        var channel = conversation.Channel;
        var serviceAddress = conversation.ServiceAddress;
        var contactAddress = conversation.ContactAddress;

        var messages = await session.Query<OmnichannelMessage, OmnichannelMessageIndex>(
                index => index.Channel == channel &&
                    index.ServiceAddress == serviceAddress &&
                    index.CustomerAddress == contactAddress &&
                    index.ConversationId == null &&
                    !index.IsInbound,
                collection: OmnichannelConstants.CollectionName)
            .ListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.ConversationId = conversation.ItemId;

            await session.SaveAsync(message, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);
        }
    }
}
