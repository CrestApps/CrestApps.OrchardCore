using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Handlers;

internal sealed class ToolInstancesAICompletionContextBuilderHandler : IAICompletionContextBuilderHandler
{
    public Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        if (context.Resource is AIProfile profile &&
            profile.TryGet<AIProfileFunctionInstancesMetadata>(out var functionInstancesMetadata))
        {
            context.Context.InstanceIds = functionInstancesMetadata.InstanceIds;
        }

        return Task.CompletedTask;
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
        => Task.CompletedTask;
}

