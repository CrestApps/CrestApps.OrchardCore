using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Handlers;

internal sealed class ChatInteractionCompletionContextBuilderHandler : IAICompletionContextBuilderHandler
{
    private readonly PromptTemplateSelectionService _promptTemplateSelectionService;

    public ChatInteractionCompletionContextBuilderHandler(PromptTemplateSelectionService promptTemplateSelectionService)
    {
        _promptTemplateSelectionService = promptTemplateSelectionService;
    }

    public async Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        if (context.Resource is not ChatInteraction interaction)
        {
            return;
        }

        context.Context.ConnectionName = interaction.ConnectionName;
        context.Context.ChatDeploymentName = interaction.ChatDeploymentName;
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
        var hasPromptTemplates = promptMetadata.Templates?.Any(selection => !string.IsNullOrWhiteSpace(selection.TemplateId)) == true;

        if (!hasPromptTemplates)
        {
            return interaction.SystemMessage;
        }

        return await _promptTemplateSelectionService.ComposeSystemMessageAsync(interaction.SystemMessage, promptMetadata);
    }
}
