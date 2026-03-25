using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Handlers;

internal sealed class AIProfileCompletionContextBuilderHandler : IAICompletionContextBuilderHandler
{
    private readonly PromptTemplateSelectionService _promptTemplateSelectionService;

    public AIProfileCompletionContextBuilderHandler(PromptTemplateSelectionService promptTemplateSelectionService)
    {
        _promptTemplateSelectionService = promptTemplateSelectionService;
    }

    public async Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        if (context.Resource is not AIProfile profile)
        {
            return;
        }

        context.Context.ChatDeploymentId = profile.ChatDeploymentId;

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

        if (profile.TryGet<FunctionInvocationMetadata>(out var functionInvocationMetadata)
            && functionInvocationMetadata.Names is { Length: > 0 })
        {
            context.Context.ToolNames = functionInvocationMetadata.Names;
        }
        else if (profile.Properties.TryGetPropertyValue("AIProfileFunctionInvocationMetadata", out var legacyNode))
        {
            // Backward compatibility: read from the legacy property key used in earlier versions.
            var legacyMetadata = legacyNode.Deserialize<FunctionInvocationMetadata>();

            if (legacyMetadata?.Names is { Length: > 0 })
            {
                context.Context.ToolNames = legacyMetadata.Names;
            }
        }

        if (profile.TryGet<AgentInvocationMetadata>(out var agentInvocationMetadata)
            && agentInvocationMetadata.Names is { Length: > 0 })
        {
            context.Context.AgentNames = agentInvocationMetadata.Names;
        }
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
        => Task.CompletedTask;

    private async Task<string> ResolveSystemMessageAsync(AIProfile profile, AIProfileMetadata metadata)
    {
        var promptMetadata = profile.As<PromptTemplateMetadata>();
        var hasPromptTemplates = promptMetadata.Templates?.Any(selection => !string.IsNullOrWhiteSpace(selection.TemplateId)) == true;

        if (!hasPromptTemplates)
        {
            return metadata.SystemMessage;
        }

        return await _promptTemplateSelectionService.ComposeSystemMessageAsync(metadata.SystemMessage, promptMetadata);
    }
}
