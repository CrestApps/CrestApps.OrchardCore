using CrestApps.AI.Prompting.Models;
using CrestApps.AI.Prompting.Parsing;
using CrestApps.AI.Prompting.Providers;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Prompting.Providers;

/// <summary>
/// Discovers AI templates from OrchardCore module directories.
/// Uses <see cref="IApplicationContext"/> to access module assets via
/// the same mechanism as OrchardCore's <c>ModuleEmbeddedFileProvider</c>,
/// ensuring reliable template discovery in both development (project references)
/// and production (NuGet/Docker) environments.
/// Files in subdirectories matching a feature ID are associated with that feature.
/// </summary>
public sealed class ModuleAITemplateProvider : IAITemplateProvider
{
    private const string PromptsDirectorySubPath = FileSystemAITemplateProvider.PromptsDirectoryPath + "/";

    private readonly IExtensionManager _extensionManager;
    private readonly IApplicationContext _applicationContext;
    private readonly IEnumerable<IAITemplateParser> _parsers;
    private readonly ILogger<ModuleAITemplateProvider> _logger;

    public ModuleAITemplateProvider(
        IExtensionManager extensionManager,
        IApplicationContext applicationContext,
        IEnumerable<IAITemplateParser> parsers,
        ILogger<ModuleAITemplateProvider> logger)
    {
        _extensionManager = extensionManager;
        _applicationContext = applicationContext;
        _parsers = parsers;
        _logger = logger;
    }

    public Task<IReadOnlyList<AITemplate>> GetTemplatesAsync()
    {
        var templates = new List<AITemplate>();
        var application = _applicationContext.Application;

        foreach (var extension in _extensionManager.GetExtensions())
        {
            var moduleId = extension.Id;
            var module = application.GetModule(moduleId);

            if (string.IsNullOrEmpty(module.Name))
            {
                continue;
            }

            var defaultFeatureId = extension.Features.FirstOrDefault()?.Id ?? moduleId;

            // Module.Root is "Areas/ModuleName/", so promptsRoot becomes
            // "Areas/ModuleName/AITemplates/Prompts/" which matches the asset path format.
            var promptsRoot = module.Root + PromptsDirectorySubPath;

            foreach (var assetPath in module.AssetPaths)
            {
                if (!assetPath.StartsWith(promptsRoot, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Get the path relative to the prompts root.
                var relativePath = assetPath[promptsRoot.Length..];

                if (string.IsNullOrEmpty(relativePath))
                {
                    continue;
                }

                var fileName = Path.GetFileName(relativePath);
                var fileExtension = Path.GetExtension(fileName);
                var parser = GetParserForExtension(fileExtension);

                if (parser == null)
                {
                    continue;
                }

                // Determine the feature ID: subdirectory name for feature-specific templates,
                // or the module's default feature for root-level templates.
                var slashIndex = relativePath.IndexOf('/');
                var featureId = slashIndex >= 0 ? relativePath[..slashIndex] : defaultFeatureId;

                try
                {
                    // Read the file via Module.GetFileInfo() — the same mechanism
                    // used by OrchardCore's ModuleEmbeddedFileProvider.
                    var fileSubPath = assetPath[module.Root.Length..];
                    var fileInfo = module.GetFileInfo(fileSubPath);

                    if (!fileInfo.Exists)
                    {
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("AI template asset '{AssetPath}' exists in module asset paths but the embedded resource was not found.", assetPath);
                        }

                        continue;
                    }

                    using var stream = fileInfo.CreateReadStream();
                    using var reader = new StreamReader(stream);
                    var content = reader.ReadToEnd();

                    var parseResult = parser.Parse(content);
                    var id = Path.GetFileNameWithoutExtension(fileName);

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
                    _logger.LogWarning(ex, "Failed to parse AI template file: {AssetPath}", assetPath);
                }
            }
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Discovered {Count} AI templates from modules.", templates.Count);
        }

        return Task.FromResult<IReadOnlyList<AITemplate>>(templates);
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
