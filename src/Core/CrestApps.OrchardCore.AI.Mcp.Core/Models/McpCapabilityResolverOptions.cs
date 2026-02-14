namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

/// <summary>
/// Configuration options for the MCP capability resolver.
/// </summary>
public sealed class McpCapabilityResolverOptions
{
    /// <summary>
    /// Gets or sets the minimum cosine similarity score required for a capability
    /// to be considered relevant when using embedding-based matching. Default is 0.3.
    /// </summary>
    public float SimilarityThreshold { get; set; } = 0.3f;

    /// <summary>
    /// Gets or sets the minimum keyword match score (0–1) required for a capability
    /// to be considered relevant when using keyword-based fallback matching. Default is 0.2.
    /// </summary>
    public float KeywordMatchThreshold { get; set; } = 0.2f;

    /// <summary>
    /// Gets or sets the maximum number of top matching capabilities to return.
    /// Default is 5.
    /// </summary>
    public int TopK { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of total capabilities across all connections
    /// below which all capabilities are returned without filtering. This ensures
    /// the intent model always has context when the capability set is small. Default is 20.
    /// </summary>
    public int IncludeAllThreshold { get; set; } = 20;
}
