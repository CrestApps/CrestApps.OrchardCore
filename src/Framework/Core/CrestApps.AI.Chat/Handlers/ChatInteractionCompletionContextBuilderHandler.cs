using CrestApps.AI.Models;
using CrestApps.AI.Prompting.Services;

namespace CrestApps.AI.Chat.Handlers;

internal sealed class ChatInteractionCompletionContextBuilderHandler : IAICompletionContextBuilderHandler
{
    private readonly IAITemplateService _aiTemplateService;

    public ChatInteractionCompletionContextBuilderHandler(IAITemplateService aiTemplateService)
    {
        _aiTemplateService = aiTemplateService;
    }

    public async Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        if (context.Resource is not ChatInteraction interaction)
        {
            return;
        }

        context.Context.ConnectionName = interaction.ConnectionName;
        context.Context.ChatDeploymentId = interaction.ChatDeploymentId;
        context.Context.SystemMessage = await ResolveSystemMessageAsync(interaction);
        context.Context.Temperature = interaction.Temperature;
        context.Context.TopP = interaction.TopP;
        context.Context.FrequencyPenalty = interaction.FrequencyPenalty;
        context.Context.PresencePenalty = interaction.PresencePenalty;
        context.Context.MaxTokens = interaction.MaxTokens;
        context.Context.PastMessagesCount = interaction.PastMessagesCount;
        context.Context.ToolNames = interaction.ToolNames?.ToArray();
        context.Context.AgentNames = interaction.AgentNames?.ToArray();
        context.Context.McpConnectionIds = interaction.McpConnectionIds?.ToArray();
        context.Context.A2AConnectionIds = interaction.A2AConnectionIds?.ToArray();

        context.Context.AdditionalProperties["InteractionId"] = interaction.ItemId;

        if (interaction.DocumentTopN.HasValue)
        {
            context.Context.AdditionalProperties["DocumentTopN"] = interaction.DocumentTopN.Value;
        }

        if (interaction.TryGet<DataSourceMetadata>(out var dataSourceMetadata))
        {
            context.Context.DataSourceId = dataSourceMetadata.DataSourceId;
        }

        if (interaction.TryGet<AIDataSourceRagMetadata>(out var ragMetadata))
        {
            context.Context.AdditionalProperties["Strictness"] = ragMetadata.Strictness;
            context.Context.AdditionalProperties["TopNDocuments"] = ragMetadata.TopNDocuments;
            context.Context.AdditionalProperties["IsInScope"] = ragMetadata.IsInScope;
            context.Context.AdditionalProperties["Filter"] = ragMetadata.Filter;
        }
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
        => Task.CompletedTask;

    private async Task<string> ResolveSystemMessageAsync(ChatInteraction interaction)
    {
        var promptMetadata = interaction.As<PromptTemplateMetadata>();
        var validTemplates = promptMetadata.Templates?
            .Where(selection => !string.IsNullOrWhiteSpace(selection.TemplateId))
            .ToList();

        if (validTemplates is not { Count: > 0 })
        {
            return interaction.SystemMessage;
        }

        var parts = new List<string>(validTemplates.Count);

        foreach (var template in validTemplates)
        {
            var rendered = await _aiTemplateService.RenderAsync(template.TemplateId, template.Parameters);

            if (!string.IsNullOrWhiteSpace(rendered))
            {
                parts.Add(rendered);
            }
        }

        if (!string.IsNullOrWhiteSpace(interaction.SystemMessage))
        {
            parts.Add(interaction.SystemMessage);
        }

        return parts.Count == 0
            ? null
            : string.Join(Environment.NewLine + Environment.NewLine, parts);
    }
}
