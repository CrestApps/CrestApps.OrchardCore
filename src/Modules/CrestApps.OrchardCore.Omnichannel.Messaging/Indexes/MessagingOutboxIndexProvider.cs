using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using OrchardCore.Entities;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Indexes;

/// <summary>
/// Indexes the outbound messages that wait for another attempt, and nothing else, so the row disappears as soon as a
/// message is sent or fails for good.
/// </summary>
public sealed class MessagingOutboxIndexProvider : IndexProvider<OmnichannelMessage>
{
    private static readonly string _queued = MessageDeliveryStatus.Queued.ToString();

    public MessagingOutboxIndexProvider()
    {
        CollectionName = OmnichannelConstants.CollectionName;
    }

    public override void Describe(DescribeContext<OmnichannelMessage> context)
    {
        context
            .For<MessagingOutboxIndex>()
            .When(message => !message.IsInbound && string.Equals(message.DeliveryStatus, _queued, StringComparison.Ordinal))
            .Map(message =>
            {
                if (!message.TryGet<OutboundDeliveryState>(out var state) || state?.NextAttemptUtc is null)
                {
                    return null;
                }

                return new MessagingOutboxIndex
                {
                    Channel = message.Channel,
                    ServiceAddress = message.ServiceAddress,
                    NextAttemptUtc = state.NextAttemptUtc.Value,
                };
            });
    }
}
