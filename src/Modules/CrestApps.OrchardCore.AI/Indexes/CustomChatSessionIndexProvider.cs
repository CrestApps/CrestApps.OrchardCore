using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Indexes;

public sealed class CustomChatSessionIndexProvider : IndexProvider<CustomChatSession>
{
    public override void Describe(DescribeContext<CustomChatSession> context)
    {
        context
            .For<CustomChatSessionIndex>()
            .Map(session => new CustomChatSessionIndex
            {
                SessionId = session.SessionId,
                CustomChatInstanceId = session.CustomChatInstanceId,
                UserId = session.UserId,
                Source = session.Source,
                DisplayText = session.Title,
                CreatedUtc = session.CreatedUtc
            });
    }
}
