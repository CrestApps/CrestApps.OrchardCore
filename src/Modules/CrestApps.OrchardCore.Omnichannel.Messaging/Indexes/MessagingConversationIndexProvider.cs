using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Indexes;

/// <summary>
/// Maps <see cref="MessagingConversation"/> documents to the <see cref="MessagingConversationIndex"/>.
/// </summary>
public sealed class MessagingConversationIndexProvider : IndexProvider<MessagingConversation>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingConversationIndexProvider"/> class.
    /// </summary>
    public MessagingConversationIndexProvider()
    {
        CollectionName = MessagingStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<MessagingConversation> context)
    {
        context
            .For<MessagingConversationIndex>()
            .Map(conversation => new MessagingConversationIndex
            {
                ItemId = conversation.ItemId,
                Channel = conversation.Channel,
                ServiceAddress = conversation.ServiceAddress,
                ContactAddress = conversation.ContactAddress,
                ContactContentItemId = conversation.ContactContentItemId,
                CustomerKey = conversation.GetCustomerKey(),
                OwnerType = conversation.OwnerType.ToString(),
                OwnerId = conversation.OwnerId,
                AssignedAgentId = conversation.AssignedAgentId,
                AssignmentStatus = conversation.AssignmentStatus.ToString(),
                Status = conversation.Status.ToString(),
                IsRead = conversation.IsRead,
                LastMessageUtc = conversation.LastMessageUtc,
                UnreadCount = conversation.UnreadCount,
                AssignedUtc = conversation.AssignedUtc,
                FirstResponseDueUtc = conversation.FirstResponseDueUtc,
            });
    }
}
