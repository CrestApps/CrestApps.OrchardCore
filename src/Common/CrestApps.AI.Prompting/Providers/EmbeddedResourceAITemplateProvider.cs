using System.Reflection;
using CrestApps.AI.Prompting.Models;
using CrestApps.AI.Prompting.Parsing;

namespace CrestApps.AI.Prompting.Providers;

/// <summary>
/// Discovers AI templates from embedded resources in a specified assembly.
/// Looks for resources matching the pattern <c>*.AITemplates.Prompts.*</c>
/// with extensions supported by registered parsers.
/// </summary>
public sealed class EmbeddedResourceAITemplateProvider : IAITemplateProvider
{
    private const string PromptsResourceSegment = ".AITemplates.Prompts.";

    private readonly Assembly _assembly;
    private readonly IEnumerable<IAITemplateParser> _parsers;
    private readonly string _source;
    private readonly string _featureId;

    public EmbeddedResourceAITemplateProvider(
        Assembly assembly,
        IEnumerable<IAITemplateParser> parsers,
        string source = null,
        string featureId = null)
    {
        _assembly = assembly;
        _parsers = parsers;
        _source = source ?? assembly.GetName().Name;
        _featureId = featureId;
    }

    public Task<IReadOnlyList<AITemplate>> GetTemplatesAsync()
    {
        var templates = new List<AITemplate>();
        var resourceNames = _assembly.GetManifestResourceNames();

        foreach (var resourceName in resourceNames)
        {
            var promptsIndex = resourceName.IndexOf(PromptsResourceSegment, StringComparison.OrdinalIgnoreCase);
            if (promptsIndex < 0)
            {
                continue;
            }

            // Find a parser that supports this resource's extension.
            var extension = GetExtension(resourceName);
            var parser = GetParserForExtension(extension);

            if (parser == null)
            {
                continue;
            }

            using var stream = _assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            var parseResult = parser.Parse(content);

            // Extract the filename portion from the resource name.
            var afterPrompts = resourceName[(promptsIndex + PromptsResourceSegment.Length)..];

            // Remove the file extension.
            var id = extension != null && afterPrompts.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                ? afterPrompts[..^extension.Length]
                : afterPrompts;

            var template = new AITemplate
            {
                Id = id,
                Metadata = parseResult.Metadata,
                Content = parseResult.Body,
                Source = _source,
                FeatureId = _featureId,
            };

            if (string.IsNullOrWhiteSpace(template.Metadata.Title))
            {
                template.Metadata.Title = id.Replace('-', ' ').Replace('.', ' ');
            }

            templates.Add(template);
        }

        return Task.FromResult<IReadOnlyList<AITemplate>>(templates);
    }

    private IAITemplateParser GetParserForExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return null;
        }

        foreach (var parser in _parsers)
        {
            foreach (var supported in parser.SupportedExtensions)
            {
                if (string.Equals(supported, extension, StringComparison.OrdinalIgnoreCase))
                {
                    return parser;
                }
            }
        }

        return null;
    }

    private static string GetExtension(string resourceName)
    {
        var lastDot = resourceName.LastIndexOf('.');
        if (lastDot <= 0)
        {
            return null;
        }

        return resourceName[lastDot..];
    }
}
