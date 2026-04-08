namespace CrestApps.Core.AI.Models;

/// <summary>
/// Metadata for data source embedding index profiles that stores embedding configuration.
/// </summary>
public sealed class DataSourceIndexProfileMetadata
{
    /// <summary>
    /// Gets or sets the deployment identifier for the embedding service.
    /// When set, the deployment resolver uses this to locate the correct
    /// provider, connection, and deployment automatically.
    /// </summary>
    public string EmbeddingDeploymentId { get; set; }

    /// <summary>
    /// Gets or sets the provider name for the embedding service.
    /// </summary>
    [Obsolete("Use EmbeddingDeploymentId instead. This property is retained for backward compatibility.")]
    public string EmbeddingProviderName { get; set; }

    /// <summary>
    /// Gets or sets the connection name for the embedding service.
    /// </summary>
    [Obsolete("Use EmbeddingDeploymentId instead. This property is retained for backward compatibility.")]
    public string EmbeddingConnectionName { get; set; }

    /// <summary>
    /// Gets or sets the deployment name for the embedding service.
    /// </summary>
    [Obsolete("Use EmbeddingDeploymentId instead. This property is retained for backward compatibility.")]
    public string EmbeddingDeploymentName { get; set; }
}
