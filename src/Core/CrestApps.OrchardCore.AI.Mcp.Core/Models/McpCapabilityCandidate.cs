namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

/// <summary>
/// Represents a single MCP capability that was matched against a user prompt
/// through semantic similarity search.
/// </summary>
public sealed class McpCapabilityCandidate
{
    /// <summary>
    /// Gets or sets the MCP connection identifier that owns this capability.
    /// </summary>
    public string ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the MCP connection.
    /// </summary>
    public string ConnectionDisplayText { get; set; }

    /// <summary>
    /// Gets or sets the name of the capability (tool, prompt, or resource).
    /// </summary>
    public string CapabilityName { get; set; }

    /// <summary>
    /// Gets or sets the description of the capability.
    /// </summary>
    public string CapabilityDescription { get; set; }

    /// <summary>
    /// Gets or sets the type of capability.
    /// </summary>
    public McpCapabilityType CapabilityType { get; set; }

    /// <summary>
    /// Gets or sets the cosine similarity score between the user prompt and
    /// this capability's embedding. Range is typically -1 to 1, where higher is more similar.
    /// </summary>
    public float SimilarityScore { get; set; }
}
