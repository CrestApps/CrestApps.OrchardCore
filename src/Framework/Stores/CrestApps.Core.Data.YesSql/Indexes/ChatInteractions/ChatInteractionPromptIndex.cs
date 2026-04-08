using CrestApps.Core.AI.Models;
using YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql.Indexes.ChatInteractions;

/// <summary>
/// Index for ChatInteractionPrompt documents to enable efficient querying by ChatInteractionId.
/// </summary>
public sealed class ChatInteractionPromptIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the ChatInteractionId this prompt belongs to.
    /// </summary>
    public string ChatInteractionId { get; set; }

    /// <summary>
    /// Gets or sets the role of the message sender.
    /// </summary>
    public string Role { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when the prompt was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

public sealed class ChatInteractionPromptIndexProvider : IndexProvider<ChatInteractionPrompt>
{
    public ChatInteractionPromptIndexProvider()
    {
        CollectionName = OrchardCoreAICollectionNames.AI;
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
