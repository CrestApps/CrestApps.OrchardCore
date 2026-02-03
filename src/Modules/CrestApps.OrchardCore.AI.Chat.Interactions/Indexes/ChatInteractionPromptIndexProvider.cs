using CrestApps.OrchardCore.AI.Chat.Interactions.Core.Indexes;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Indexes;

internal sealed class ChatInteractionPromptIndexProvider : IndexProvider<ChatInteractionPrompt>
{
    public ChatInteractionPromptIndexProvider()
    {
        CollectionName = AIConstants.CollectionName;
    }

    public override void Describe(DescribeContext<ChatInteractionPrompt> context)
    {
        context
            .For<ChatInteractionPromptIndex>()
            .Map(prompt => new ChatInteractionPromptIndex
            {
                ItemId = prompt.ItemId,
                ChatInteractionId = prompt.ChatInteractionId,
                Role = prompt.Role.Value,
                CreatedUtc = prompt.CreatedUtc,
            });
    }
}
