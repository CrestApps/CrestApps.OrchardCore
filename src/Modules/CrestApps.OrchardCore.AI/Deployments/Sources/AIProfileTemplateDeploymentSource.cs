using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

internal sealed class AIProfileTemplateDeploymentSource : DeploymentSourceBase<AIProfileTemplateDeploymentStep>
{
    private readonly INamedCatalog<AIProfileTemplate> _templatesCatalog;

    public AIProfileTemplateDeploymentSource(INamedCatalog<AIProfileTemplate> templatesCatalog)
    {
        _templatesCatalog = templatesCatalog;
    }

    protected override async Task ProcessAsync(AIProfileTemplateDeploymentStep step, DeploymentPlanResult result)
    {
        var templates = await _templatesCatalog.GetAllAsync();

        var templatesData = new JsonArray();

        var templateNames = step.IncludeAll
            ? []
            : step.TemplateNames ?? [];

        foreach (var template in templates)
        {
            if (templateNames.Length > 0 && !templateNames.Contains(template.Name))
            {
                continue;
            }

            var templateInfo = new JsonObject()
            {
                { "ItemId", template.ItemId },
                { "Name", template.Name },
                { "DisplayText", template.DisplayText },
                { "Description", template.Description },
                { "Category", template.Category },
                { "IsListable", template.IsListable },
                { "SystemMessage", template.SystemMessage },
                { "WelcomeMessage", template.WelcomeMessage },
                { "PromptTemplate", template.PromptTemplate },
                { "PromptSubject", template.PromptSubject },
                { "ConnectionName", template.ConnectionName },
                { "OrchestratorName", template.OrchestratorName },
                { "CreatedUtc", template.CreatedUtc },
                { "OwnerId", template.OwnerId },
                { "Author", template.Author },
                { "Properties", template.Properties?.DeepClone() },
            };

            if (template.ProfileType.HasValue)
            {
                templateInfo.Add("ProfileType", template.ProfileType.Value.ToString());
            }

            if (template.TitleType.HasValue)
            {
                templateInfo.Add("TitleType", template.TitleType.Value.ToString());
            }

            if (template.Temperature.HasValue)
            {
                templateInfo.Add("Temperature", template.Temperature.Value);
            }

            if (template.TopP.HasValue)
            {
                templateInfo.Add("TopP", template.TopP.Value);
            }

            if (template.FrequencyPenalty.HasValue)
            {
                templateInfo.Add("FrequencyPenalty", template.FrequencyPenalty.Value);
            }

            if (template.PresencePenalty.HasValue)
            {
                templateInfo.Add("PresencePenalty", template.PresencePenalty.Value);
            }

            if (template.MaxOutputTokens.HasValue)
            {
                templateInfo.Add("MaxOutputTokens", template.MaxOutputTokens.Value);
            }

            if (template.PastMessagesCount.HasValue)
            {
                templateInfo.Add("PastMessagesCount", template.PastMessagesCount.Value);
            }

            if (template.ToolNames?.Length > 0)
            {
                templateInfo.Add("ToolNames", new JsonArray(template.ToolNames.Select(t => (JsonNode)JsonValue.Create(t)).ToArray()));
            }

            templatesData.Add(templateInfo);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["templates"] = templatesData,
        });
    }
}
