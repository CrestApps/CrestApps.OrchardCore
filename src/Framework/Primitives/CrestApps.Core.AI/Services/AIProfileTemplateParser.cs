using CrestApps.Core.AI.Models;
using CrestApps.Core.Templates.Models;
using CrestApps.Core.Templates.Parsing;

namespace CrestApps.Core.AI.Services;

/// <summary>
/// Converts parsed markdown profile templates into <see cref="AIProfileTemplate"/> models.
/// </summary>
public static class AIProfileTemplateParser
{
    public static AIProfileTemplate Parse(string id, TemplateParseResult parseResult)
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
            Enum.TryParse<AgentAvailability>(agentAvailabilityStr, true, out var agentAvailability))
        {
            profileMetadata.AgentAvailability = agentAvailability;
        }

        template.Put(profileMetadata);

        return template;
    }

    public static ITemplateParser GetParserForExtension(IEnumerable<ITemplateParser> parsers, string extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return null;
        }

        foreach (var parser in parsers)
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
