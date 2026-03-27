namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// Metadata for Chat Interaction document index profiles that stores embedding configuration.
/// </summary>
public class ChatInteractionIndexProfileMetadata
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
}
