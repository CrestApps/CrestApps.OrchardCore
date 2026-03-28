namespace CrestApps.AI.Prompting.Models;

/// <summary>
/// Represents the result of parsing a prompt template file,
/// separating front matter metadata from the template body.
/// </summary>
public sealed class AITemplateParseResult
{
    /// <summary>
    /// Gets or sets the parsed metadata from the front matter section.
    /// </summary>
    public AITemplateMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the template body content (after front matter).
    /// </summary>
    public string Body { get; set; }
}
