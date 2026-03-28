using CrestApps.AI.Models;
using CrestApps.Data.YesSql.Indexes;
using YesSql.Indexes;

namespace CrestApps.Mvc.Web.Indexes;

public sealed class ChatInteractionPromptIndex : CatalogItemIndex
{
    public string ChatInteractionId { get; set; }

    public string Role { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public sealed class ChatInteractionPromptIndexProvider : IndexProvider<ChatInteractionPrompt>
{
    public override void Describe(DescribeContext<ChatInteractionPrompt> context)
    {
        context.For<ChatInteractionPromptIndex>()
            .Map(prompt => new ChatInteractionPromptIndex
            {
                ItemId = prompt.ItemId,
                ChatInteractionId = prompt.ChatInteractionId,
                Role = prompt.Role.Value,
                CreatedUtc = prompt.CreatedUtc,
            });
    }
}
