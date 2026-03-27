using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

/// <summary>
/// View model for editing Chat Interaction index profile embedding settings.
/// </summary>
public class EditChatInteractionIndexProfileViewModel
{
    /// <summary>
    /// Gets or sets the connection name for the embedding service.
    /// </summary>
    public string EmbeddingConnection { get; set; }

    /// <summary>
    /// Gets or sets the available embedding connections.
    /// </summary>
    public IList<SelectListItem> EmbeddingConnections { get; set; } = [];
}
