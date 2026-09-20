using CrestApps.Core.Data.YesSql.Omnichannel.Indexes;
using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.Omnichannel.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Indexes;

internal sealed class OmnichannelMessageIndexProvider : IndexProvider<OmnichannelMessage>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelMessageIndexProvider"/> class.
    /// </summary>
    public OmnichannelMessageIndexProvider()
    {
        CollectionName = OmnichannelConstants.CollectionName;
    }

    public override void Describe(DescribeContext<OmnichannelMessage> context)
    {
        context
            .For<OmnichannelMessageIndex>()
            .Map(message => new OmnichannelMessageIndex
            {
                Channel = message.Channel,
                CustomerAddress = message.CustomerAddress,
                ServiceAddress = message.ServiceAddress,
                CreatedUtc = message.CreatedUtc,
                IsInbound = message.IsInbound,
                ConversationId = message.ConversationId,
                ProviderMessageId = message.ProviderMessageId,
            });
    }
}
