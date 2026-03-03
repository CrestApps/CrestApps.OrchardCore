namespace CrestApps.AI.Prompting.Models;

/// <summary>
/// Represents an AI prompt template with metadata and content.
/// </summary>
public sealed class AITemplate
{
    /// <summary>
    /// Gets or sets the unique identifier of the prompt template.
    /// Derived from the filename (without extension) for file-based templates.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the metadata extracted from the front matter section.
    /// </summary>
    public AITemplateMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the raw template content (the body after front matter).
    /// May contain Liquid syntax for dynamic rendering.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets the source identifier (e.g., assembly name, module name, or "code").
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the optional feature identifier this prompt is associated with.
    /// When set, the prompt is only available when the feature is enabled.
    /// </summary>
    public string FeatureId { get; set; }
}
