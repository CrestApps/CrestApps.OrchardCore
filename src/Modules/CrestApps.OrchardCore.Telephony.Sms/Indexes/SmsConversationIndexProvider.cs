using CrestApps.OrchardCore.Telephony.Sms.Core;
using CrestApps.OrchardCore.Telephony.Sms.Core.Indexes;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Telephony.Sms.Indexes;

/// <summary>
/// Maps <see cref="SmsConversation"/> documents to the <see cref="SmsConversationIndex"/>.
/// </summary>
public sealed class SmsConversationIndexProvider : IndexProvider<SmsConversation>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SmsConversationIndexProvider"/> class.
    /// </summary>
    public SmsConversationIndexProvider()
    {
        CollectionName = TelephonySmsStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<SmsConversation> context)
    {
        context
            .For<SmsConversationIndex>()
            .Map(conversation => new SmsConversationIndex
            {
                ItemId = conversation.ItemId,
                ServiceAddress = conversation.ServiceAddress,
                CustomerAddress = conversation.CustomerAddress,
                OwnerType = conversation.OwnerType.ToString(),
                OwnerId = conversation.OwnerId,
                AssignedAgentId = conversation.AssignedAgentId,
                AssignmentStatus = conversation.AssignmentStatus.ToString(),
                Status = conversation.Status.ToString(),
                IsRead = conversation.IsRead,
                LastMessageUtc = conversation.LastMessageUtc,
            });
    }
}
