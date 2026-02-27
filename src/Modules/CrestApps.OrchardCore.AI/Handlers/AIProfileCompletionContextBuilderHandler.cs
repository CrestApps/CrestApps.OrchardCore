using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Handlers;

internal sealed class AIProfileCompletionContextBuilderHandler : IAICompletionContextBuilderHandler
{
    private readonly IAITemplateService _aiTemplateService;

    public AIProfileCompletionContextBuilderHandler(IAITemplateService aiTemplateService)
    {
        _aiTemplateService = aiTemplateService;
    }

    public async Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        if (context.Resource is not AIProfile profile)
        {
            return;
        }

        context.Context.ConnectionName = profile.ConnectionName;
        context.Context.DeploymentId = profile.DeploymentId;

        if (profile.TryGet<AIProfileMetadata>(out var metadata))
        {
            context.Context.SystemMessage = await ResolveSystemMessageAsync(profile, metadata);
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
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
        => Task.CompletedTask;

    private async Task<string> ResolveSystemMessageAsync(AIProfile profile, AIProfileMetadata metadata)
    {
        var promptMetadata = profile.As<PromptTemplateMetadata>();

        if (string.IsNullOrEmpty(promptMetadata.TemplateId))
        {
            return metadata.SystemMessage;
        }

        return await _aiTemplateService.RenderAsync(promptMetadata.TemplateId, promptMetadata.Parameters);
    }
}
