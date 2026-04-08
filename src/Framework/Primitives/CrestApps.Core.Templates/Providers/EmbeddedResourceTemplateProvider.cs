using System.Reflection;
using CrestApps.Core.Templates.Models;
using CrestApps.Core.Templates.Parsing;

namespace CrestApps.Core.Templates.Providers;

/// <summary>
/// Discovers templates from embedded resources in a specified assembly.
/// Looks for resources matching the pattern <c>*.Templates.Prompts.*</c>
/// with extensions supported by registered parsers.
/// </summary>
public sealed class EmbeddedResourceTemplateProvider : ITemplateProvider
{
    private const string PromptsResourceSegment = ".Templates.Prompts.";

    // OrchardCore Module Targets use '>' as the path separator in embedded resource logical names.
    private const string OrchardCorePromptsResourceSegment = ".Templates>Prompts>";

    private readonly Assembly _assembly;
    private readonly IEnumerable<ITemplateParser> _parsers;
    private readonly string _source;
    private readonly string _featureId;

    public EmbeddedResourceTemplateProvider(
        Assembly assembly,
        IEnumerable<ITemplateParser> parsers,
        string source = null,
        string featureId = null)
    {
        _assembly = assembly;
        _parsers = parsers;
        _source = source ?? assembly.GetName().Name;
        _featureId = featureId;
    }

    public Task<IReadOnlyList<Template>> GetTemplatesAsync()
    {
        var templates = new List<Template>();
        var resourceNames = _assembly.GetManifestResourceNames();

        foreach (var resourceName in resourceNames)
        {
            var promptsIndex = resourceName.IndexOf(PromptsResourceSegment, StringComparison.OrdinalIgnoreCase);
            var segmentLength = PromptsResourceSegment.Length;

            if (promptsIndex < 0)
            {
                promptsIndex = resourceName.IndexOf(OrchardCorePromptsResourceSegment, StringComparison.OrdinalIgnoreCase);
                segmentLength = OrchardCorePromptsResourceSegment.Length;
            }

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
            var afterPrompts = resourceName[(promptsIndex + segmentLength)..];

            // Remove the file extension.
            var id = extension != null && afterPrompts.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                ? afterPrompts[..^extension.Length]
                : afterPrompts;

            var template = new Template
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

        return Task.FromResult<IReadOnlyList<Template>>(templates);
    }

    private ITemplateParser GetParserForExtension(string extension)
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
