using CrestApps.Core.Templates.Models;

namespace CrestApps.Core.Templates;

/// <summary>
/// Options for configuring template registration and discovery.
/// </summary>
public sealed class TemplateOptions
{
    /// <summary>
    /// Gets the collection of prompt templates registered via code.
    /// </summary>
    public IList<Template> Templates { get; } = [];
    /// <summary>
    /// Gets the collection of file system paths to scan for prompt template files.
    /// Each path should contain a <c>Templates/Prompts</c> directory structure.
    /// </summary>
    public IList<string> DiscoveryPaths { get; } = [];
    /// <summary>
    /// Gets the well-known metadata keys that are mapped to <see cref="TemplateMetadata"/> properties.
    /// Keys not in this set are placed in <see cref="TemplateMetadata.AdditionalProperties"/>.
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
    public TemplateOptions AddTemplate(Template template)
    {
        ArgumentNullException.ThrowIfNull(template);

        Templates.Add(template);

        return this;
    }
    /// <summary>
    /// Registers a prompt template with minimal configuration.
    /// </summary>
    public TemplateOptions AddTemplate(string id, string content, Action<TemplateMetadata> configureMetadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var template = new Template
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
    public TemplateOptions AddDiscoveryPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        DiscoveryPaths.Add(path);

        return this;
    }
}
