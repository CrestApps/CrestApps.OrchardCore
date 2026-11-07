using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Indexes;

/// <summary>
/// Maps the delivery log.
/// </summary>
public sealed class EmailDeliveryLogIndexProvider : IndexProvider<EmailDeliveryLogEntry>
{
    public EmailDeliveryLogIndexProvider()
    {
        CollectionName = EmailChannelConstants.DeliverabilityCollectionName;
    }

    public override void Describe(DescribeContext<EmailDeliveryLogEntry> context)
    {
        context
            .For<EmailDeliveryLogIndex>()
            .Map(entry => new EmailDeliveryLogIndex
            {
                AddressId = entry.AddressId,
                Kind = (int)entry.Kind,
                Recipient = Truncate(entry.Recipient, EmailDeliveryLogIndex.RecipientLength),
                RecipientDomain = Truncate(entry.RecipientDomain, EmailDeliveryLogIndex.RecipientLength),
                MessageId = Truncate(entry.MessageId, EmailDeliveryLogIndex.MessageIdLength),
                EventId = Truncate(entry.EventId, EmailDeliveryLogIndex.EventIdLength),
                IsBulk = entry.IsBulk,
                OccurredUtc = entry.OccurredUtc,
            });
    }

    internal static string Truncate(string value, int length)
        => value is null || value.Length <= length ? value : value[..length];
}

/// <summary>
/// Maps the suppression list.
/// </summary>
public sealed class EmailSuppressionIndexProvider : IndexProvider<EmailSuppression>
{
    public EmailSuppressionIndexProvider()
    {
        CollectionName = EmailChannelConstants.DeliverabilityCollectionName;
    }

    public override void Describe(DescribeContext<EmailSuppression> context)
    {
        context
            .For<EmailSuppressionIndex>()
            .Map(suppression => new EmailSuppressionIndex
            {
                Address = EmailDeliveryLogIndexProvider.Truncate(suppression.Address, EmailDeliveryLogIndex.RecipientLength),
                Reason = (int)suppression.Reason,
                CreatedUtc = suppression.CreatedUtc,
            });
    }
}

/// <summary>
/// Maps the sending state.
/// </summary>
public sealed class EmailSendingStateIndexProvider : IndexProvider<EmailSendingState>
{
    public EmailSendingStateIndexProvider()
    {
        CollectionName = EmailChannelConstants.DeliverabilityCollectionName;
    }

    public override void Describe(DescribeContext<EmailSendingState> context)
    {
        context
            .For<EmailSendingStateIndex>()
            .Map(state => new EmailSendingStateIndex
            {
                AddressId = state.AddressId,
            });
    }
}
