using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.DataSources.Handlers;

internal sealed class DataSourceAICompletionContextBuilderHandler : IAICompletionContextBuilderHandler
{
    public Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        if (context.Resource is AIProfile profile &&
            profile.TryGet<DataSourceMetadata>(out var dataSourceMetadata) &&
            !string.IsNullOrEmpty(dataSourceMetadata.DataSourceId))
        {
            context.Context.DataSourceId = dataSourceMetadata.DataSourceId;

            // Store DataSourceId in the invocation context so the DataSourceSearchTool can access it.
            var invocationContext = AIInvocationScope.Current;

            if (invocationContext is not null)
            {
                invocationContext.DataSourceId = dataSourceMetadata.DataSourceId;
            }
        }

        return Task.CompletedTask;
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
        => Task.CompletedTask;
}
