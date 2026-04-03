using CrestApps.AI.Models;
using CrestApps.Data.YesSql.Indexes;
using YesSql.Indexes;

namespace CrestApps.Mvc.Web.Areas.ChatInteractions.Indexes;

public sealed class ChatInteractionIndex : CatalogItemIndex
{
    public string UserId { get; set; }

    public string Title { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public sealed class ChatInteractionIndexProvider : IndexProvider<ChatInteraction>
{
    public override void Describe(DescribeContext<ChatInteraction> context)
    {
        context.For<ChatInteractionIndex>()
            .Map(interaction => new ChatInteractionIndex
            {
                ItemId = interaction.ItemId,
                UserId = interaction.OwnerId,
                Title = interaction.Title?[..Math.Min(interaction.Title.Length, 255)],
                CreatedUtc = interaction.CreatedUtc,
            });
    }
}
