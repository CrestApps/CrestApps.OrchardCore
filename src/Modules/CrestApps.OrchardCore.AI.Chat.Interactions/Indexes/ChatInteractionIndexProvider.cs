using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.Support;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Indexes;

internal sealed class ChatInteractionIndexProvider : IndexProvider<ChatInteraction>
{
    public ChatInteractionIndexProvider()
    {
        CollectionName = AIConstants.AICollectionName;
    }

    public override void Describe(DescribeContext<ChatInteraction> context)
    {
        context
            .For<ChatInteractionIndex>()
            .Map(interaction =>
            {
                return new ChatInteractionIndex
                {
                    ItemId = interaction.ItemId,
                    UserId = interaction.OwnerId,
                    Title = Str.Truncate(interaction.Title, 255),
                    CreatedUtc = interaction.CreatedUtc,
                };
            });
    }
}
