using CrestApps.AI.Prompting.Models;
using CrestApps.AI.Prompting.Parsing;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.Providers;

/// <summary>
/// Discovers AI profile templates from the App_Data directory.
/// Scans both the global <c>AITemplates/Profiles/</c> folder under App_Data
/// and the tenant-specific <c>Sites/{tenantName}/AITemplates/Profiles/</c> folder.
/// </summary>
internal sealed class AppDataAIProfileTemplateProvider : IAIProfileTemplateProvider
{
    private const string _aiTemplatesDirectory = "AITemplates";
    private const string _profilesSubDirectory = "Profiles";

    private readonly ShellOptions _shellOptions;
    private readonly ShellSettings _shellSettings;
    private readonly IEnumerable<IAITemplateParser> _parsers;
    private readonly ILogger _logger;

    public AppDataAIProfileTemplateProvider(
        Microsoft.Extensions.Options.IOptions<ShellOptions> shellOptions,
        ShellSettings shellSettings,
        IEnumerable<IAITemplateParser> parsers,
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

        // Scan the global App_Data/AITemplates/Profiles/ directory.
        var globalProfilesDir = Path.Combine(
            _shellOptions.ShellsApplicationDataPath,
            _aiTemplatesDirectory,
            _profilesSubDirectory);

        DiscoverTemplates(globalProfilesDir, templates);

        // Scan the tenant-specific App_Data/Sites/{tenantName}/AITemplates/Profiles/ directory.
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

                var template = ConvertToProfileTemplate(id, parseResult);
                templates.Add(template);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse AI profile template file: {FilePath}", file);
            }
        }
    }

    private static AIProfileTemplate ConvertToProfileTemplate(string id, AITemplateParseResult parseResult)
    {
        var metadata = parseResult.Metadata;
        var props = metadata.AdditionalProperties;

        var template = new AIProfileTemplate
        {
            ItemId = id,
            Name = id,
            Source = AITemplateSources.Profile,
            DisplayText = metadata.Title ?? id.Replace('-', ' ').Replace('.', ' '),
            Description = metadata.Description,
            Category = metadata.Category,
            IsListable = metadata.IsListable,
        };

        if (props.TryGetValue(nameof(AIProfileTemplate.Source), out var sourceStr) &&
            !string.IsNullOrWhiteSpace(sourceStr))
        {
            template.Source = sourceStr;
        }

        var profileMetadata = new ProfileTemplateMetadata
        {
            SystemMessage = parseResult.Body,
        };

        if (props.TryGetValue(nameof(ProfileTemplateMetadata.ProfileType), out var profileTypeStr) &&
            Enum.TryParse<AIProfileType>(profileTypeStr, true, out var profileType))
        {
            profileMetadata.ProfileType = profileType;
        }

        if (props.TryGetValue(nameof(ProfileTemplateMetadata.ChatDeploymentName), out var chatDeploymentName))
        {
            profileMetadata.ChatDeploymentName = chatDeploymentName;
        }

        if (props.TryGetValue(nameof(ProfileTemplateMetadata.UtilityDeploymentName), out var utilityDeploymentName))
        {
            profileMetadata.UtilityDeploymentName = utilityDeploymentName;
        }

        if (props.TryGetValue(nameof(ProfileTemplateMetadata.OrchestratorName), out var orchestratorName))
        {
            profileMetadata.OrchestratorName = orchestratorName;
        }

        if (props.TryGetValue(nameof(ProfileTemplateMetadata.WelcomeMessage), out var welcomeMessage))
        {
            profileMetadata.WelcomeMessage = welcomeMessage;
        }

        if (props.TryGetValue(nameof(ProfileTemplateMetadata.PromptTemplate), out var promptTemplate))
        {
            profileMetadata.PromptTemplate = promptTemplate;
        }

        if (props.TryGetValue(nameof(ProfileTemplateMetadata.PromptSubject), out var promptSubject))
        {
            profileMetadata.PromptSubject = promptSubject;
        }

        if (props.TryGetValue(nameof(ProfileTemplateMetadata.TitleType), out var titleTypeStr) &&
            Enum.TryParse<AISessionTitleType>(titleTypeStr, true, out var titleType))
        {
            profileMetadata.TitleType = titleType;
        }

        if (props.TryGetValue(nameof(ProfileTemplateMetadata.Temperature), out var tempStr) &&
            float.TryParse(tempStr, out var temp))
        {
            profileMetadata.Temperature = temp;
        }

        if (props.TryGetValue(nameof(ProfileTemplateMetadata.TopP), out var topPStr) &&
            float.TryParse(topPStr, out var topP))
        {
            profileMetadata.TopP = topP;
        }

        if (props.TryGetValue(nameof(ProfileTemplateMetadata.FrequencyPenalty), out var freqStr) &&
            float.TryParse(freqStr, out var freq))
        {
            profileMetadata.FrequencyPenalty = freq;
        }

        if (props.TryGetValue(nameof(ProfileTemplateMetadata.PresencePenalty), out var presStr) &&
            float.TryParse(presStr, out var pres))
        {
            profileMetadata.PresencePenalty = pres;
        }

        if (props.TryGetValue("MaxTokens", out var maxTokensStr) &&
            int.TryParse(maxTokensStr, out var maxTokens))
        {
            profileMetadata.MaxOutputTokens = maxTokens;
        }
        else if (props.TryGetValue(nameof(ProfileTemplateMetadata.MaxOutputTokens), out var maxOutputStr) &&
                 int.TryParse(maxOutputStr, out var maxOutput))
        {
            profileMetadata.MaxOutputTokens = maxOutput;
        }

        if (props.TryGetValue(nameof(ProfileTemplateMetadata.PastMessagesCount), out var pastStr) &&
            int.TryParse(pastStr, out var past))
        {
            profileMetadata.PastMessagesCount = past;
        }

        if (props.TryGetValue(nameof(ProfileTemplateMetadata.ToolNames), out var toolNamesStr) &&
            !string.IsNullOrWhiteSpace(toolNamesStr))
        {
            profileMetadata.ToolNames = toolNamesStr
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        if (props.TryGetValue(nameof(ProfileTemplateMetadata.AgentNames), out var agentNamesStr) &&
            !string.IsNullOrWhiteSpace(agentNamesStr))
        {
            profileMetadata.AgentNames = agentNamesStr
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        if (props.TryGetValue("ProfileDescription", out var profileDescription))
        {
            profileMetadata.Description = profileDescription;
        }

        if (props.TryGetValue(nameof(ProfileTemplateMetadata.AgentAvailability), out var agentAvailabilityStr) &&
            Enum.TryParse<AgentAvailability>(agentAvailabilityStr, ignoreCase: true, out var agentAvailability))
        {
            profileMetadata.AgentAvailability = agentAvailability;
        }

        template.Put(profileMetadata);

        return template;
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
