using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Indexes;

/// <summary>
/// Provides ominchannel activity AI chat session index functionality.
/// </summary>
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
