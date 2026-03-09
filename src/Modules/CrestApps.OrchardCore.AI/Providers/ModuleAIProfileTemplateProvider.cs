using CrestApps.AI.Prompting.Models;
using CrestApps.AI.Prompting.Parsing;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Providers;

/// <summary>
/// Discovers AI profile templates from OrchardCore module directories.
/// Scans <c>AITemplates/Profiles/</c> folders embedded in modules and converts
/// parsed templates into <see cref="AIProfileTemplate"/> instances.
/// </summary>
internal sealed class ModuleAIProfileTemplateProvider : IAIProfileTemplateProvider
{
    internal const string ProfilesDirectoryPath = "AITemplates/Profiles";

    private const string _profilesDirectorySubPath = ProfilesDirectoryPath + "/";

    private readonly IExtensionManager _extensionManager;
    private readonly IApplicationContext _applicationContext;
    private readonly IEnumerable<IAITemplateParser> _parsers;
    private readonly ILogger _logger;

    public ModuleAIProfileTemplateProvider(
        IExtensionManager extensionManager,
        IApplicationContext applicationContext,
        IEnumerable<IAITemplateParser> parsers,
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

                    var template = ConvertToProfileTemplate(id, parseResult);
                    templates.Add(template);
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

    private static AIProfileTemplate ConvertToProfileTemplate(string id, AITemplateParseResult parseResult)
    {
        var metadata = parseResult.Metadata;
        var props = metadata.AdditionalProperties;

        var template = new AIProfileTemplate
        {
            ItemId = id,
            Name = id,
            DisplayText = metadata.Title ?? id.Replace('-', ' ').Replace('.', ' '),
            Description = metadata.Description,
            Category = metadata.Category,
            IsListable = metadata.IsListable,
            SystemMessage = parseResult.Body,
        };

        if (props.TryGetValue(nameof(AIProfileTemplate.ProfileType), out var profileTypeStr) &&
            Enum.TryParse<AIProfileType>(profileTypeStr, true, out var profileType))
        {
            template.ProfileType = profileType;
        }

        if (props.TryGetValue(nameof(AIProfileTemplate.ConnectionName), out var connectionName))
        {
            template.ConnectionName = connectionName;
        }

        if (props.TryGetValue(nameof(AIProfileTemplate.OrchestratorName), out var orchestratorName))
        {
            template.OrchestratorName = orchestratorName;
        }

        if (props.TryGetValue(nameof(AIProfileTemplate.WelcomeMessage), out var welcomeMessage))
        {
            template.WelcomeMessage = welcomeMessage;
        }

        if (props.TryGetValue(nameof(AIProfileTemplate.PromptTemplate), out var promptTemplate))
        {
            template.PromptTemplate = promptTemplate;
        }

        if (props.TryGetValue(nameof(AIProfileTemplate.PromptSubject), out var promptSubject))
        {
            template.PromptSubject = promptSubject;
        }

        if (props.TryGetValue(nameof(AIProfileTemplate.TitleType), out var titleTypeStr) &&
            Enum.TryParse<AISessionTitleType>(titleTypeStr, true, out var titleType))
        {
            template.TitleType = titleType;
        }

        if (props.TryGetValue(nameof(AIProfileTemplate.Temperature), out var tempStr) &&
            float.TryParse(tempStr, out var temp))
        {
            template.Temperature = temp;
        }

        if (props.TryGetValue(nameof(AIProfileTemplate.TopP), out var topPStr) &&
            float.TryParse(topPStr, out var topP))
        {
            template.TopP = topP;
        }

        if (props.TryGetValue(nameof(AIProfileTemplate.FrequencyPenalty), out var freqStr) &&
            float.TryParse(freqStr, out var freq))
        {
            template.FrequencyPenalty = freq;
        }

        if (props.TryGetValue(nameof(AIProfileTemplate.PresencePenalty), out var presStr) &&
            float.TryParse(presStr, out var pres))
        {
            template.PresencePenalty = pres;
        }

        if (props.TryGetValue("MaxTokens", out var maxTokensStr) &&
            int.TryParse(maxTokensStr, out var maxTokens))
        {
            template.MaxOutputTokens = maxTokens;
        }
        else if (props.TryGetValue(nameof(AIProfileTemplate.MaxOutputTokens), out var maxOutputStr) &&
                 int.TryParse(maxOutputStr, out var maxOutput))
        {
            template.MaxOutputTokens = maxOutput;
        }

        if (props.TryGetValue(nameof(AIProfileTemplate.PastMessagesCount), out var pastStr) &&
            int.TryParse(pastStr, out var past))
        {
            template.PastMessagesCount = past;
        }

        if (props.TryGetValue(nameof(AIProfileTemplate.ToolNames), out var toolNamesStr) &&
            !string.IsNullOrWhiteSpace(toolNamesStr))
        {
            template.ToolNames = toolNamesStr
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

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
