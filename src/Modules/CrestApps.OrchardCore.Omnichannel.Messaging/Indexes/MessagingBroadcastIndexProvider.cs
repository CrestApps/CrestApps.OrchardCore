using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Indexes;

/// <summary>
/// Maps <see cref="MessagingBroadcast"/> documents to the <see cref="MessagingBroadcastIndex"/>.
/// </summary>
public sealed class MessagingBroadcastIndexProvider : IndexProvider<MessagingBroadcast>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingBroadcastIndexProvider"/> class.
    /// </summary>
    public MessagingBroadcastIndexProvider()
    {
        CollectionName = MessagingStorage.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<MessagingBroadcast> context)
    {
        context
            .For<MessagingBroadcastIndex>()
            .Map(broadcast => new MessagingBroadcastIndex
            {
                ItemId = broadcast.ItemId,
                Name = broadcast.Name,
                Status = broadcast.Status.ToString(),
            });
    }
}
