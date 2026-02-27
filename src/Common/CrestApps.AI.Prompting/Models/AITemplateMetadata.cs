namespace CrestApps.AI.Prompting.Models;

/// <summary>
/// Metadata extracted from the front matter section of a prompt template file.
/// </summary>
public sealed class AITemplateMetadata
{
    /// <summary>
    /// Gets or sets the display title for the prompt template.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the description of what this prompt template does.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets whether this prompt appears in listing UIs.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool IsListable { get; set; } = true;

    /// <summary>
    /// Gets or sets the category for grouping prompts in the UI.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets additional metadata properties for extensibility.
    /// Used for future use cases such as AI Profile parameters.
    /// </summary>
    public Dictionary<string, string> AdditionalProperties { get; set; } = [];
}
