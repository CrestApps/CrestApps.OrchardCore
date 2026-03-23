using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.AI.Models;
using CrestApps.Services;
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
                { "Source", template.Source },
                { "DisplayText", template.DisplayText },
                { "Description", template.Description },
                { "Category", template.Category },
                { "IsListable", template.IsListable },
                { "CreatedUtc", template.CreatedUtc },
                { "OwnerId", template.OwnerId },
                { "Author", template.Author },
                { "Properties", template.Properties != null ? JsonSerializer.SerializeToNode(template.Properties) : null },
            };

            templatesData.Add(templateInfo);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["templates"] = templatesData,
        });
    }
}
