namespace CrestApps.Core.AI.Models;

/// <summary>
/// Metadata for content item references in a chat interaction prompt.
/// Use <see cref="the Put extension method"/> to attach this to a <see cref="ChatInteractionPrompt"/>.
/// </summary>
public sealed class ChatInteractionPromptContentMetadata
{
    /// <summary>
    /// Gets or sets the content item IDs referenced in this prompt.
    /// </summary>
    public IList<string> ContentItemIds { get; set; } = [];
}
