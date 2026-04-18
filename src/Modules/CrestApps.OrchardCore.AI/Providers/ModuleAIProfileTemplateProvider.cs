using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Templates.Parsing;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Extensions;

using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Providers;

/// <summary>
/// Discovers AI profile templates from OrchardCore module directories.
/// Scans <c>Templates/Profiles/</c> folders embedded in modules and converts
/// parsed templates into <see cref="AIProfileTemplate"/> instances.
/// </summary>
internal sealed class ModuleAIProfileTemplateProvider : IAIProfileTemplateProvider
{
    internal const string ProfilesDirectoryPath = "Templates/Profiles";

    private const string _profilesDirectorySubPath = ProfilesDirectoryPath + "/";

    private readonly IExtensionManager _extensionManager;
    private readonly IApplicationContext _applicationContext;
    private readonly IEnumerable<ITemplateParser> _parsers;

    private readonly ILogger _logger;

    public ModuleAIProfileTemplateProvider(
        IExtensionManager extensionManager,
        IApplicationContext applicationContext,
        IEnumerable<ITemplateParser> parsers,
        ILogger<ModuleAIProfileTemplateProvider> logger)
    {
        _extensionManager = extensionManager;
        _applicationContext = applicationContext;
        _parsers = parsers;
        _logger = logger;

    }

    public Task<IReadOnlyList<AIProfileTemplate>> GetTemplatesAsync()
    {
        var templates = new List<AIProfileTemplate>();

        var application = _applicationContext.Application;

        foreach (var extension in _extensionManager.GetExtensions())
        {
            var moduleId = extension.Id;

            var module = application.GetModule(moduleId);

            if (string.IsNullOrEmpty(module.Name))
            {
                continue;

            }

            var profilesRoot = module.Root + _profilesDirectorySubPath;

            foreach (var assetPath in module.AssetPaths)
            {
                if (!assetPath.StartsWith(profilesRoot, StringComparison.OrdinalIgnoreCase))
                {
                    continue;

                }

                var relativePath = assetPath[profilesRoot.Length..];

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

                try
                {
                    var fileSubPath = assetPath[module.Root.Length..];

                    var fileInfo = module.GetFileInfo(fileSubPath);

                    if (!fileInfo.Exists)
                    {
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            _logger.LogDebug("AI profile template asset '{AssetPath}' exists in module asset paths but the embedded resource was not found.", assetPath);

                        }

                        continue;

                    }

                    using var stream = fileInfo.CreateReadStream();
                    using var reader = new StreamReader(stream);

                    var content = reader.ReadToEnd();

                    var parseResult = parser.Parse(content);

                    var id = Path.GetFileNameWithoutExtension(fileName);

                    templates.Add(AIProfileTemplateParser.Parse(id, parseResult));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse AI profile template file: {AssetPath}", assetPath);
                }
            }

        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Discovered {Count} AI profile templates from modules.", templates.Count);

        }

        return Task.FromResult<IReadOnlyList<AIProfileTemplate>>(templates);

    }
    private ITemplateParser GetParserForExtension(string extension)
    {
        return AIProfileTemplateParser.GetParserForExtension(_parsers, extension);
    }
}
