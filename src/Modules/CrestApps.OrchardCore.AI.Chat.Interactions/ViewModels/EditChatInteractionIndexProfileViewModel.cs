using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

/// <summary>
/// View model for editing Chat Interaction index profile embedding settings.
/// </summary>
public class EditChatInteractionIndexProfileViewModel
{
    /// <summary>
    /// Gets or sets the provider name for the embedding service.
    /// </summary>
    public string EmbeddingProviderName { get; set; }

    /// <summary>
    /// Gets or sets the connection name for the embedding service.
    /// </summary>
    public string EmbeddingConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the deployment name for the embedding service.
    /// </summary>
    public string EmbeddingDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the available embedding connections.
    /// </summary>
    public IEnumerable<SelectListItem> EmbeddingConnections { get; set; } = [];
}
