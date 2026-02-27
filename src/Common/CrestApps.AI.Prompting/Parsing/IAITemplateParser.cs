using CrestApps.AI.Prompting.Models;

namespace CrestApps.AI.Prompting.Parsing;

/// <summary>
/// Parses AI template content into metadata and body.
/// Implement this interface to add support for additional file formats
/// (e.g., YAML, JSON) alongside the built-in Markdown front matter parser.
/// </summary>
public interface IAITemplateParser
{
    /// <summary>
    /// Gets the file extensions this parser supports (e.g., <c>".md"</c>, <c>".yaml"</c>).
    /// Extensions should include the leading dot and be lowercase.
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Parses the raw content of a template file, separating
    /// metadata from the template body.
    /// </summary>
    /// <param name="rawContent">The full content of the template file.</param>
    /// <returns>A parse result containing metadata and body.</returns>
    AITemplateParseResult Parse(string rawContent);
}
