using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

internal sealed class AIDeploymentDeploymentSource : DeploymentSourceBase<AIDeploymentDeploymentStep>
{
    // Prices are exported without their empty options, so an exported plan reads cleanly and imports as written.
    private static readonly JsonSerializerOptions _pricingSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly INamedSourceCatalog<AIDeployment> _deploymentCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDeploymentDeploymentSource"/> class.
    /// </summary>
    /// <param name="deploymentCatalog">The deployment catalog.</param>
    public AIDeploymentDeploymentSource(INamedSourceCatalog<AIDeployment> deploymentCatalog)
    {
        _deploymentCatalog = deploymentCatalog;
    }

    protected override async Task ProcessAsync(AIDeploymentDeploymentStep step, DeploymentPlanResult result)
    {
        var deployments = await _deploymentCatalog.GetAllAsync();

        var deploymentData = new JsonArray();

        var deploymentNames = step.IncludeAll
            ? []
            : step.DeploymentNames ?? [];

        foreach (var deployment in deployments)
        {
            if (deploymentNames.Length > 0 && !deploymentNames.Contains(deployment.Name))
            {
                continue;
            }

            // Pricing is exported on its own, readable section rather than inside Properties.
            var properties = JsonSerializer.SerializeToNode(deployment.Properties)?.AsObject();
            properties?.Remove(nameof(AIDeploymentPricing));

            var deploymentInfo = new JsonObject()
            {
                { "ItemId", deployment.ItemId },
                { "Name", deployment.Name },
                { "ModelName", deployment.ModelName },
                { "ClientName", deployment.Source },
                { "ProviderName" , deployment.Source },
                { "ConnectionName", deployment.ConnectionName },
                { "Author", deployment.Author },
                { "OwnerId", deployment.OwnerId },
                { "CreatedUtc" , deployment.CreatedUtc },
                { "Properties", properties },
            };

            if (deployment.GetPricing() is { } pricing)
            {
                deploymentInfo["Pricing"] = JsonSerializer.SerializeToNode(pricing, _pricingSerializerOptions);
            }

            deploymentData.Add(deploymentInfo);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            // The recipe schema names the list "Deployments"; the step reads either spelling.
            ["Deployments"] = deploymentData,
        });
    }
}
