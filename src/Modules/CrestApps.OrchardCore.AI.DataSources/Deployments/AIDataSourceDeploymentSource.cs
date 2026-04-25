using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.DataSources;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.DataSources.Deployments;

internal sealed class AIDataSourceDeploymentSource : DeploymentSourceBase<AIDataSourceDeploymentStep>
{
    private readonly IAIDataSourceStore _store;

    public AIDataSourceDeploymentSource(IAIDataSourceStore store)
    {
        _store = store;
    }

    protected override async Task ProcessAsync(AIDataSourceDeploymentStep step, DeploymentPlanResult result)
    {
        var dataSources = await _store.GetAllAsync();

        var sourceObjects = new JsonArray();

        var sourceIds = step.IncludeAll
        ? []
        : step.SourceIds ?? [];

        foreach (var dataSource in dataSources)
        {
            if (sourceIds.Length > 0 && !sourceIds.Contains(dataSource.ItemId))
            {
                continue;
            }

            var sourceObject = new JsonObject()
            {
                { "ItemId", dataSource.ItemId },
                { "DisplayText", dataSource.DisplayText },
                { "SourceIndexProfileName", dataSource.SourceIndexProfileName },
                { "AIKnowledgeBaseIndexProfileName", dataSource.AIKnowledgeBaseIndexProfileName },
                { "KeyFieldName", dataSource.KeyFieldName },
                { "TitleFieldName", dataSource.TitleFieldName },
                { "ContentFieldName", dataSource.ContentFieldName },
                { "CreatedUtc", dataSource.CreatedUtc },
                { "OwnerId", dataSource.OwnerId },
                { "Author", dataSource.Author },
                { "Properties", dataSource.Properties is not null ? JsonSerializer.SerializeToNode(dataSource.Properties) : null },
            };

            sourceObjects.Add(sourceObject);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["DataSources"] = sourceObjects,
        });
    }
}
