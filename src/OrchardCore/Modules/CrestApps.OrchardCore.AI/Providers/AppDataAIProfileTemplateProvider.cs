using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Templates.Parsing;
using Microsoft.Extensions.Logging;

using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Providers;

/// <summary>
/// Discovers AI profile templates from the App_Data directory.
/// Scans both the global <c>Templates/Profiles/</c> folder under App_Data
/// and the tenant-specific <c>Sites/{tenantName}/Templates/Profiles/</c> folder.
/// </summary>
internal sealed class AppDataAIProfileTemplateProvider : IAIProfileTemplateProvider
{
    private const string _aiTemplatesDirectory = "AITemplates";

    private const string _profilesSubDirectory = "Profiles";

    private readonly ShellOptions _shellOptions;
    private readonly ShellSettings _shellSettings;
    private readonly IEnumerable<ITemplateParser> _parsers;

    private readonly ILogger _logger;

    public AppDataAIProfileTemplateProvider(
        Microsoft.Extensions.Options.IOptions<ShellOptions> shellOptions,
        ShellSettings shellSettings,
        IEnumerable<ITemplateParser> parsers,
        ILogger<AppDataAIProfileTemplateProvider> logger)
    {
        _shellOptions = shellOptions.Value;
        _shellSettings = shellSettings;
        _parsers = parsers;
        _logger = logger;

    }

    public Task<IReadOnlyList<AIProfileTemplate>> GetTemplatesAsync()
    {

        var templates = new List<AIProfileTemplate>();

        // Scan the global App_Data/Templates/Profiles/ directory.
        var globalProfilesDir = Path.Combine(
            _shellOptions.ShellsApplicationDataPath,
            _aiTemplatesDirectory,

            _profilesSubDirectory);

        DiscoverTemplates(globalProfilesDir, templates);

        // Scan the tenant-specific App_Data/Sites/{tenantName}/Templates/Profiles/ directory.
        var tenantProfilesDir = Path.Combine(
            _shellOptions.ShellsApplicationDataPath,
            _shellOptions.ShellsContainerName,
            _shellSettings.Name,
            _aiTemplatesDirectory,

            _profilesSubDirectory);

        DiscoverTemplates(tenantProfilesDir, templates);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Discovered {Count} AI profile templates from App_Data directories.", templates.Count);

        }

        return Task.FromResult<IReadOnlyList<AIProfileTemplate>>(templates);

    }

    private void DiscoverTemplates(string directory, List<AIProfileTemplate> templates)
    {
        if (!Directory.Exists(directory))
        {
            return;

        }

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

                templates.Add(AIProfileTemplateParser.Parse(id, parseResult));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse AI profile template file: {FilePath}", file);
            }
        }

    }
    private ITemplateParser GetParserForExtension(string extension)
    {
        return AIProfileTemplateParser.GetParserForExtension(_parsers, extension);
    }
}
