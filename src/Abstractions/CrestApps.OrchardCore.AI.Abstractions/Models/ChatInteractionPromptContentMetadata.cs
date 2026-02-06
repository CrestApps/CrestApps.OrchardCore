namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Metadata for content item references in a chat interaction prompt.
/// Use <see cref="OrchardCore.Entities.EntityExtensions.Put{T}(OrchardCore.Entities.IEntity, T)"/> to attach this to a <see cref="ChatInteractionPrompt"/>.
/// </summary>
public sealed class ChatInteractionPromptContentMetadata
{
    /// <summary>
    /// Gets or sets the content item IDs referenced in this prompt.
    /// </summary>
    public IList<string> ContentItemIds { get; set; } = [];
}
