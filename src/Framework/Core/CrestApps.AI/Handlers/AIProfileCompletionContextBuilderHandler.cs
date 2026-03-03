using CrestApps.AI.Models;
using CrestApps.AI.Prompting.Services;

namespace CrestApps.AI.Handlers;

/// <summary>
/// Populates the <see cref="AICompletionContext"/> from <see cref="AIProfile"/> settings
/// including connection, deployment, metadata parameters, and tool names.
/// </summary>
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

        var metadata = profile.GetSettings<AIProfileMetadata>();

        context.Context.SystemMessage = await ResolveSystemMessageAsync(profile, metadata);
        context.Context.Temperature = metadata.Temperature;
        context.Context.TopP = metadata.TopP;
        context.Context.FrequencyPenalty = metadata.FrequencyPenalty;
        context.Context.PresencePenalty = metadata.PresencePenalty;
        context.Context.MaxTokens = metadata.MaxTokens;
        context.Context.PastMessagesCount = metadata.PastMessagesCount;
        context.Context.UseCaching = metadata.UseCaching;

        if (profile.TryGetSettings<AIProfileFunctionInvocationMetadata>(out var functionInvocationMetadata))
        {
            context.Context.ToolNames = functionInvocationMetadata.Names;
        }
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
        => Task.CompletedTask;

    private async Task<string> ResolveSystemMessageAsync(AIProfile profile, AIProfileMetadata metadata)
    {
        if (profile.TryGetSettings<PromptTemplateMetadata>(out var promptMetadata)
            && !string.IsNullOrEmpty(promptMetadata.TemplateId))
        {
            return await _aiTemplateService.RenderAsync(promptMetadata.TemplateId, promptMetadata.Parameters);
        }

        return metadata.SystemMessage;
    }
}
