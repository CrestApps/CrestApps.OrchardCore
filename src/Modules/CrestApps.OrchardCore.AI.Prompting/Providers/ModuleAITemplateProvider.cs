using CrestApps.AI.Prompting.Models;
using CrestApps.AI.Prompting.Parsing;
using CrestApps.AI.Prompting.Providers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Extensions;

namespace CrestApps.OrchardCore.AI.Prompting.Providers;

/// <summary>
/// Discovers AI templates from OrchardCore module directories.
/// Scans each module's <c>AITemplates/Prompts/</c> directory for files
/// matching registered parser extensions.
/// Files in subdirectories matching a feature ID are associated with that feature.
/// </summary>
public sealed class ModuleAITemplateProvider : IAITemplateProvider
{
    private readonly IExtensionManager _extensionManager;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IEnumerable<IAITemplateParser> _parsers;
    private readonly ILogger<ModuleAITemplateProvider> _logger;

    public ModuleAITemplateProvider(
        IExtensionManager extensionManager,
        IHostEnvironment hostEnvironment,
        IEnumerable<IAITemplateParser> parsers,
        ILogger<ModuleAITemplateProvider> logger)
    {
        _extensionManager = extensionManager;
        _hostEnvironment = hostEnvironment;
        _parsers = parsers;
        _logger = logger;
    }

    public Task<IReadOnlyList<AITemplate>> GetTemplatesAsync()
    {
        var templates = new List<AITemplate>();

        foreach (var extension in _extensionManager.GetExtensions())
        {
            var extensionSubPath = extension.SubPath;
            if (string.IsNullOrEmpty(extensionSubPath))
            {
                continue;
            }

            var promptsPath = Path.Combine(extensionSubPath, FileSystemAITemplateProvider.PromptsDirectoryPath);

            var fileProvider = _hostEnvironment.ContentRootFileProvider;
            var promptsDirectory = fileProvider.GetDirectoryContents(promptsPath);

            if (!promptsDirectory.Exists)
            {
                continue;
            }

            var moduleId = extension.Id;
            var defaultFeatureId = extension.Features.FirstOrDefault()?.Id ?? moduleId;

            // Scan root-level prompts (associated with the module's default feature).
            DiscoverTemplates(promptsDirectory, defaultFeatureId, moduleId, templates);

            // Scan subdirectories for feature-specific prompts.
            foreach (var entry in promptsDirectory)
            {
                if (!entry.IsDirectory)
                {
                    continue;
                }

                var featureId = entry.Name;
                var featureDir = fileProvider.GetDirectoryContents(Path.Combine(promptsPath, featureId));

                if (featureDir.Exists)
                {
                    DiscoverTemplates(featureDir, featureId, moduleId, templates);
                }
            }
        }

        return Task.FromResult<IReadOnlyList<AITemplate>>(templates);
    }

    private void DiscoverTemplates(
        Microsoft.Extensions.FileProviders.IDirectoryContents directory,
        string featureId,
        string moduleId,
        List<AITemplate> templates)
    {
        foreach (var fileInfo in directory)
        {
            if (fileInfo.IsDirectory)
            {
                continue;
            }

            var extension = Path.GetExtension(fileInfo.Name);
            var parser = GetParserForExtension(extension);

            if (parser == null)
            {
                continue;
            }

            try
            {
                using var stream = fileInfo.CreateReadStream();
                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();

                var parseResult = parser.Parse(content);
                var id = Path.GetFileNameWithoutExtension(fileInfo.Name);

                var template = new AITemplate
                {
                    Id = id,
                    Metadata = parseResult.Metadata,
                    Content = parseResult.Body,
                    Source = moduleId,
                    FeatureId = featureId,
                };

                if (string.IsNullOrWhiteSpace(template.Metadata.Title))
                {
                    template.Metadata.Title = id.Replace('-', ' ').Replace('.', ' ');
                }

                templates.Add(template);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse AI template file: {FileName}", fileInfo.Name);
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
