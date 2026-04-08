namespace CrestApps.Core.Templates.Models;

/// <summary>
/// Represents the result of parsing a prompt template file,
/// separating front matter metadata from the template body.
/// </summary>
public sealed class TemplateParseResult
{
    /// <summary>
    /// Gets or sets the parsed metadata from the front matter section.
    /// </summary>
    public TemplateMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the template body content (after front matter).
    /// </summary>
    public string Body { get; set; }
}
