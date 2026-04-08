using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;

namespace CrestApps.Core.AI.Handlers;

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

            invocationContext?.DataSourceId = dataSourceMetadata.DataSourceId;
        }

        return Task.CompletedTask;
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
        => Task.CompletedTask;
}
