using System.Text.Json;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Templates.Services;

namespace CrestApps.Core.AI.Handlers;

/// <summary>
/// Populates the <see cref="AICompletionContext"/> from <see cref="AIProfile"/> settings
/// including connection, deployment, metadata parameters, and tool names.
/// </summary>
internal sealed class AIProfileCompletionContextBuilderHandler : IAICompletionContextBuilderHandler
{
    private readonly ITemplateService _aiTemplateService;

    public AIProfileCompletionContextBuilderHandler(ITemplateService aiTemplateService)
    {
        _aiTemplateService = aiTemplateService;
    }

    public async Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        if (context.Resource is not AIProfile profile)
        {
            return;
        }

        context.Context.ConnectionName = profile.GetLegacyConnectionName();
        context.Context.ChatDeploymentName = profile.ChatDeploymentName;
        context.Context.UtilityDeploymentName = profile.UtilityDeploymentName;

        var metadata = profile.As<AIProfileMetadata>();

        context.Context.SystemMessage = await ResolveSystemMessageAsync(profile, metadata);
        context.Context.Temperature = metadata.Temperature;
        context.Context.TopP = metadata.TopP;
        context.Context.FrequencyPenalty = metadata.FrequencyPenalty;
        context.Context.PresencePenalty = metadata.PresencePenalty;
        context.Context.MaxTokens = metadata.MaxTokens;
        context.Context.PastMessagesCount = metadata.PastMessagesCount;
        context.Context.UseCaching = metadata.UseCaching;

        if (profile.TryGet<FunctionInvocationMetadata>(out var functionInvocationMetadata))
        {
            context.Context.ToolNames = functionInvocationMetadata.Names;
        }

        if (context.Context.ToolNames is not { Length: > 0 } &&
            profile.Settings.TryGetPropertyValue("AIProfileFunctionInvocationMetadata", out var legacyNode))
        {
            var legacyMetadata = legacyNode.Deserialize<FunctionInvocationMetadata>();

            if (legacyMetadata?.Names is { Length: > 0 })
            {
                context.Context.ToolNames = legacyMetadata.Names;
            }
        }

        if (profile.TryGet<AgentInvocationMetadata>(out var agentInvocationMetadata))
        {
            context.Context.AgentNames = agentInvocationMetadata.Names;
        }
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
        => Task.CompletedTask;

    private async Task<string> ResolveSystemMessageAsync(AIProfile profile, AIProfileMetadata metadata)
    {
        if (profile.TryGet<PromptTemplateMetadata>(out var promptMetadata))
        {
            var validTemplates = promptMetadata.Templates?
                .Where(t => !string.IsNullOrWhiteSpace(t.TemplateId))
                .ToList();

            if (validTemplates is { Count: > 0 })
            {
                var rendered = new List<string>(validTemplates.Count);

                foreach (var template in validTemplates)
                {
                    rendered.Add(await _aiTemplateService.RenderAsync(template.TemplateId, template.Parameters));
                }

                if (!string.IsNullOrWhiteSpace(metadata.SystemMessage))
                {
                    rendered.Add(metadata.SystemMessage);
                }

                return string.Join("\n\n", rendered);
            }
        }

        return metadata.SystemMessage;
    }
}
