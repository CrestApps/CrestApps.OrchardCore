using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Indexes;

public sealed class OminchannelActivityAIChatSessionIndexProvider : IndexProvider<AIChatSession>
{
    public override void Describe(DescribeContext<AIChatSession> context)
    {
        context
            .For<OminchannelActivityAIChatSessionIndex>()
            .Map(session =>
            {
                if (!session.TryGet<OminchannelActivityMetadata>(out var metadata) || string.IsNullOrEmpty(metadata.ActivityId))
                {
                    return null;
                }

                return new OminchannelActivityAIChatSessionIndex
                {
                    SessionId = session.SessionId,
                    ActivityId = metadata.ActivityId,
                };
            });
    }
}
