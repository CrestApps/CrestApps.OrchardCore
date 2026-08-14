using CrestApps.Core.Templates.Models;
using CrestApps.Core.Templates.Parsing;
using CrestApps.Core.Templates.Providers;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Prompting.Providers;

/// <summary>
/// Discovers templates from OrchardCore module directories.
/// Uses <see cref="IApplicationContext"/> to access module assets via
/// the same mechanism as OrchardCore's <c>ModuleEmbeddedFileProvider</c>,
/// ensuring reliable template discovery in both development (project references)
/// and production (NuGet/Docker) environments.
/// Files in subdirectories matching a feature ID are associated with that feature.
/// </summary>
public sealed class ModuleTemplateProvider : ITemplateProvider
{
    private const string PromptsDirectorySubPath = PromptsFileSystemTemplateProvider.PromptsDirectoryPath + "/";

    private readonly IExtensionManager _extensionManager;
    private readonly IApplicationContext _applicationContext;
    private readonly IEnumerable<ITemplateParser> _parsers;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleTemplateProvider"/> class.
    /// </summary>
    /// <param name="extensionManager">The extension manager.</param>
    /// <param name="applicationContext">The application context.</param>
    /// <param name="parsers">The parsers.</param>
    /// <param name="logger">The logger.</param>
    public ModuleTemplateProvider(
        IExtensionManager extensionManager,
        IApplicationContext applicationContext,
        IEnumerable<ITemplateParser> parsers,
        ILogger<ModuleTemplateProvider> logger)
    {
        _extensionManager = extensionManager;
        _applicationContext = applicationContext;
        _parsers = parsers;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves the templates async.
    /// </summary>
    public Task<IReadOnlyList<Template>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var templates = new List<Template>();
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
            // "Areas/ModuleName/Templates/Prompts/" which matches the asset path format.
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
                            _logger.LogDebug("template asset '{AssetPath}' exists in module asset paths but the embedded resource was not found.", assetPath);
                        }

                        continue;
                    }

                    using var stream = fileInfo.CreateReadStream();
                    using var reader = new StreamReader(stream);
                    var content = reader.ReadToEnd();

                    var parseResult = parser.Parse(content);
                    var id = Path.GetFileNameWithoutExtension(fileName);

                    var template = new Template
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
                    _logger.LogWarning(ex, "Failed to parse template file: {AssetPath}", assetPath);
                }
            }
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Discovered {Count} templates from modules.", templates.Count);
        }

        return Task.FromResult<IReadOnlyList<Template>>(templates);
    }

    private ITemplateParser GetParserForExtension(string extension)
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
