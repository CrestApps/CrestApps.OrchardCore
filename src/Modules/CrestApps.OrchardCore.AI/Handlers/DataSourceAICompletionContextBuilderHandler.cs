using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Handlers;

internal sealed class DataSourceAICompletionContextBuilderHandler : IAICompletionContextBuilderHandler
{
    public Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        if (context.Profile.TryGet<AIProfileDataSourceMetadata>(out var dataSourceMetadata))
        {
            context.Context.DataSourceType = dataSourceMetadata.DataSourceType;
            context.Context.DataSourceId = dataSourceMetadata.DataSourceId;
        }

        return Task.CompletedTask;
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
        => Task.CompletedTask;
}

