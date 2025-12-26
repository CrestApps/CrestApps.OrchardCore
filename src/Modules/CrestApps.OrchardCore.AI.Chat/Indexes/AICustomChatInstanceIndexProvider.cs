using CrestApps.OrchardCore.AI.Chat.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Chat.Indexes;

public sealed class AICustomChatInstanceIndexProvider : IndexProvider<AICustomChatInstance>
{
    public AICustomChatInstanceIndexProvider()
    {
        CollectionName = AICustomChatConstants.CollectionName;
    }

    public override void Describe(DescribeContext<AICustomChatInstance> context)
    {
        context.For<AICustomChatInstanceIndex>()
            .Map(instance => new AICustomChatInstanceIndex
            {
                ItemId = instance.ItemId,
                Source = instance.Source,
                DisplayText = instance.DisplayText,
                UserId = instance.UserId,
                CreatedUtc = instance.CreatedUtc,
            });
    }
}
