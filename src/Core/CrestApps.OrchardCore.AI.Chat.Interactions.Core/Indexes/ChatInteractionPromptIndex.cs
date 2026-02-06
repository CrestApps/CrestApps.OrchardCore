using CrestApps.OrchardCore.YesSql.Core.Indexes;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core.Indexes;

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
