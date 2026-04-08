using CrestApps.Core.AI.Models;
using YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql.Indexes.AIChat;

/// <summary>
/// Index for AIChatSessionPrompt documents to enable efficient querying by SessionId.
/// </summary>
public sealed class AIChatSessionPromptIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the SessionId this prompt belongs to.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the role of the message sender.
    /// </summary>
    public string Role { get; set; }

    /// <summary>
    /// Gets or sets the UTC date and time when the prompt was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

public sealed class AIChatSessionPromptIndexProvider : IndexProvider<AIChatSessionPrompt>
{
    public AIChatSessionPromptIndexProvider()
    {
        CollectionName = OrchardCoreAICollectionNames.AI;
    }

    public override void Describe(DescribeContext<AIChatSessionPrompt> context)
    {
        context
            .For<AIChatSessionPromptIndex>()
            .Map(prompt => new AIChatSessionPromptIndex
            {
                ItemId = prompt.ItemId,
                SessionId = prompt.SessionId,
                Role = prompt.Role.Value,
                CreatedUtc = prompt.CreatedUtc,
            });
    }
}
