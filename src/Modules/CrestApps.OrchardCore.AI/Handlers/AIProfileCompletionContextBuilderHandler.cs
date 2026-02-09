using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Handlers;

internal sealed class AIProfileCompletionContextBuilderHandler : IAICompletionContextBuilderHandler
{
    public Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        if (context.Resource is not AIProfile profile)
        {
            return Task.CompletedTask;
        }

        context.Context.ConnectionName = profile.ConnectionName;
        context.Context.DeploymentId = profile.DeploymentId;

        if (profile.TryGet<AIProfileMetadata>(out var metadata))
        {
            context.Context.SystemMessage = metadata.SystemMessage;
            context.Context.Temperature = metadata.Temperature;
            context.Context.TopP = metadata.TopP;
            context.Context.FrequencyPenalty = metadata.FrequencyPenalty;
            context.Context.PresencePenalty = metadata.PresencePenalty;
            context.Context.MaxTokens = metadata.MaxTokens;
            context.Context.PastMessagesCount = metadata.PastMessagesCount;
            context.Context.UseCaching = metadata.UseCaching;
        }

        if (profile.TryGet<AIProfileFunctionInvocationMetadata>(out var functionInvocationMetadata))
        {
            context.Context.ToolNames = functionInvocationMetadata.Names;
        }

        return Task.CompletedTask;
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
        => Task.CompletedTask;
}
