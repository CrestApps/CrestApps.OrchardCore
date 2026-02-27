using CrestApps.AI.Prompting.Models;

namespace CrestApps.AI.Prompting;

/// <summary>
/// Options for configuring AI prompt template registration and discovery.
/// </summary>
public sealed class AITemplateOptions
{
    /// <summary>
    /// Gets the collection of prompt templates registered via code.
    /// </summary>
    public IList<AITemplate> Templates { get; } = [];

    /// <summary>
    /// Gets the collection of file system paths to scan for prompt template files.
    /// Each path should contain an <c>AI/Prompts</c> directory structure.
    /// </summary>
    public IList<string> DiscoveryPaths { get; } = [];

    /// <summary>
    /// Gets the well-known metadata keys that are mapped to <see cref="AITemplateMetadata"/> properties.
    /// Keys not in this set are placed in <see cref="AITemplateMetadata.AdditionalProperties"/>.
    /// Additional keys can be registered for future extensibility (e.g., AI Profile parameters).
    /// </summary>
    public HashSet<string> ReservedMetadataKeys { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "Title",
        "Description",
        "IsListable",
        "Category",
    };

    /// <summary>
    /// Registers a prompt template from code.
    /// </summary>
    public AITemplateOptions AddTemplate(AITemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        Templates.Add(template);

        return this;
    }

    /// <summary>
    /// Registers a prompt template with minimal configuration.
    /// </summary>
    public AITemplateOptions AddTemplate(string id, string content, Action<AITemplateMetadata> configureMetadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var template = new AITemplate
        {
            Id = id,
            Content = content,
            Source = "code",
        };

        configureMetadata?.Invoke(template.Metadata);

        Templates.Add(template);

        return this;
    }

    /// <summary>
    /// Adds a file system path to scan for prompt template files.
    /// </summary>
    public AITemplateOptions AddDiscoveryPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        DiscoveryPaths.Add(path);

        return this;
    }
}
