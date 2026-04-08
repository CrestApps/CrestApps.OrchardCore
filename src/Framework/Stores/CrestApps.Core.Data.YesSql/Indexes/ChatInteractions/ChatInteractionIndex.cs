using CrestApps.Core.AI.Models;
using CrestApps.Core.Support;
using YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql.Indexes.ChatInteractions;

public sealed class ChatInteractionIndex : CatalogItemIndex
{
    public string UserId { get; set; }

    public string Title { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public sealed class ChatInteractionIndexProvider : IndexProvider<ChatInteraction>
{
    public ChatInteractionIndexProvider()
    {
        CollectionName = OrchardCoreAICollectionNames.AI;
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
