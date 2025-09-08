using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Indexes;

internal sealed class OmnichannelMessageIndexProvider : IndexProvider<OmnichannelMessage>
{
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
                IsInbound = message.IsInbound
            });
    }
}
