using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Chat.Indexes;

public class CustomChatPartIndexProvider : IndexProvider<CustomChatSession>
{
    public override void Describe(DescribeContext<CustomChatSession> context)
    {
        context.For<AICustomChatSessionIndex>()
            .Map(x => new AICustomChatSessionIndex
            {
                SessionId = x.SessionId,
                CustomChatInstanceId = x.CustomChatInstanceId,
                UserId = x.UserId,
                Source = x.Source,
                DisplayText = x.Title,
                CreatedUtc = x.CreatedUtc
            });
    }
}
