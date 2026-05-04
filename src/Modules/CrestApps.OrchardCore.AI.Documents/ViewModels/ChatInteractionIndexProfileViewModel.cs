using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

/// <summary>
/// View model for editing Chat Interaction index profile embedding settings.
/// </summary>
public class ChatInteractionIndexProfileViewModel
{
    /// <summary>
    /// Gets or sets the selected embedding deployment name.
    /// </summary>
    public string EmbeddingDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the available embedding deployments.
    /// </summary>
    public IList<SelectListItem> EmbeddingDeployments { get; set; } = [];
}
