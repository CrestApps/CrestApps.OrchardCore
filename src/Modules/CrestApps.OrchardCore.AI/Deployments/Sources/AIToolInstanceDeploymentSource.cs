using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Tools.Handlers;
using Microsoft.Extensions.Logging;
using OrchardCore.Deployment;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Deployments.Sources;

internal sealed class AIToolInstanceDeploymentSource : DeploymentSourceBase<AIToolInstanceDeploymentStep>
{
    private readonly INamedCatalog<AIToolInstance> _instancesCatalog;
    private readonly IEnumerable<IAIToolInstanceHandler> _handlers;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolInstanceDeploymentSource"/> class.
    /// </summary>
    /// <param name="instancesCatalog">The catalog used to retrieve the AI tool instances to export.</param>
    /// <param name="handlers">The collection of handlers invoked while each tool instance is exported.</param>
    /// <param name="logger">The logger instance for this source.</param>
    public AIToolInstanceDeploymentSource(
        INamedCatalog<AIToolInstance> instancesCatalog,
        IEnumerable<IAIToolInstanceHandler> handlers,
        ILogger<AIToolInstanceDeploymentSource> logger)
    {
        _instancesCatalog = instancesCatalog;
        _handlers = handlers;
        _logger = logger;
    }

    protected override async Task ProcessAsync(AIToolInstanceDeploymentStep step, DeploymentPlanResult result)
    {
        var instances = await _instancesCatalog.GetAllAsync();

        var instanceObjects = new JsonArray();

        var instanceIds = step.IncludeAll
        ? []
        : step.InstanceIds ?? [];

        foreach (var instance in instances)
        {
            if (instanceIds.Length > 0 && !instanceIds.Contains(instance.ItemId))
            {
                continue;
            }

            var instanceObject = new JsonObject()
            {
                { "ItemId", instance.ItemId },
                { "Source", instance.Source },
                { "Name", instance.Name },
                { "Description", instance.Description },
                { "CreatedUtc", instance.CreatedUtc },
                { "OwnerId", instance.OwnerId },
                { "Author", instance.Author },
                { "Properties", JsonSerializer.SerializeToNode(instance.Properties) },
            };

            var exportingContext = new ExportingAIToolInstanceContext(instance, instanceObject);

            _handlers.Invoke((handler, context) => handler.Exporting(context), exportingContext, _logger);

            instanceObjects.Add(instanceObject);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["instances"] = instanceObjects,
        });
    }
}
