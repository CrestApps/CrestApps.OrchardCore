using CrestApps.AI.Prompting.Models;
using CrestApps.AI.Prompting.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.AI.Prompting.Providers;

/// <summary>
/// Discovers AI templates from the file system.
/// Scans configured paths for <c>AITemplates/Prompts/</c> files matching registered parser extensions.
/// </summary>
public sealed class FileSystemAITemplateProvider : IAITemplateProvider
{
    /// <summary>
    /// The directory name within a project where prompt templates are stored.
    /// </summary>
    public const string PromptsDirectoryPath = "AITemplates/Prompts";

    private readonly AITemplateOptions _options;
    private readonly IEnumerable<IAITemplateParser> _parsers;
    private readonly ILogger<FileSystemAITemplateProvider> _logger;

    public FileSystemAITemplateProvider(
        IOptions<AITemplateOptions> options,
        IEnumerable<IAITemplateParser> parsers,
        ILogger<FileSystemAITemplateProvider> logger)
    {
        _options = options.Value;
        _parsers = parsers;
        _logger = logger;
    }

    public Task<IReadOnlyList<AITemplate>> GetTemplatesAsync()
    {
        var templates = new List<AITemplate>();

        foreach (var basePath in _options.DiscoveryPaths)
        {
            var promptsDir = Path.Combine(basePath, PromptsDirectoryPath.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(promptsDir))
            {
                continue;
            }

            DiscoverTemplates(promptsDir, featureId: null, basePath, templates);

            // Scan subdirectories for feature-specific prompts.
            foreach (var subDir in Directory.GetDirectories(promptsDir))
            {
                var featureId = Path.GetFileName(subDir);
                DiscoverTemplates(subDir, featureId, basePath, templates);
            }
        }

        return Task.FromResult<IReadOnlyList<AITemplate>>(templates);
    }

    private void DiscoverTemplates(string directory, string featureId, string sourcePath, List<AITemplate> templates)
    {
        foreach (var file in Directory.GetFiles(directory))
        {
            var extension = Path.GetExtension(file);
            var parser = GetParserForExtension(extension);

            if (parser == null)
            {
                continue;
            }

            try
            {
                var content = File.ReadAllText(file);
                var parseResult = parser.Parse(content);
                var id = Path.GetFileNameWithoutExtension(file);

                var template = new AITemplate
                {
                    Id = id,
                    Metadata = parseResult.Metadata,
                    Content = parseResult.Body,
                    Source = sourcePath,
                    FeatureId = featureId,
                };

                // Use filename as title if no title in front matter.
                if (string.IsNullOrWhiteSpace(template.Metadata.Title))
                {
                    template.Metadata.Title = id.Replace('-', ' ').Replace('.', ' ');
                }

                templates.Add(template);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse AI template file: {FilePath}", file);
            }
        }
    }

    private IAITemplateParser GetParserForExtension(string extension)
    {
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
}
