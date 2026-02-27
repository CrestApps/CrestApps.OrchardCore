using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Handlers;

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
        context.Context.DeploymentId = interaction.DeploymentId;
        context.Context.SystemMessage = await ResolveSystemMessageAsync(interaction);
        context.Context.Temperature = interaction.Temperature;
        context.Context.TopP = interaction.TopP;
        context.Context.FrequencyPenalty = interaction.FrequencyPenalty;
        context.Context.PresencePenalty = interaction.PresencePenalty;
        context.Context.MaxTokens = interaction.MaxTokens;
        context.Context.PastMessagesCount = interaction.PastMessagesCount;
        context.Context.ToolNames = interaction.ToolNames?.ToArray();
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

        if (string.IsNullOrEmpty(promptMetadata.TemplateId))
        {
            return interaction.SystemMessage;
        }

        return await _aiTemplateService.RenderAsync(promptMetadata.TemplateId, promptMetadata.Parameters);
    }
}
