namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Represents a single capability summary from a pre-intent resolution pass.
/// This is a generic, provider-agnostic representation used to give the intent
/// detector contextual information about available external capabilities.
/// </summary>
public sealed class CapabilitySummary
{
    /// <summary>
    /// Gets or sets the identifier of the source (e.g., MCP connection ID) that owns this capability.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the source.
    /// </summary>
    public string SourceDisplayText { get; set; }

    /// <summary>
    /// Gets or sets the name of the capability.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description of the capability.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the semantic similarity score between the user prompt
    /// and this capability. Higher values indicate stronger relevance.
    /// </summary>
    public float Score { get; set; }
}
