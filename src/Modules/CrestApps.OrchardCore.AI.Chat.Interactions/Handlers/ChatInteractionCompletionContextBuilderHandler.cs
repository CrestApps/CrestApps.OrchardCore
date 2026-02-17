using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Handlers;

internal sealed class ChatInteractionCompletionContextBuilderHandler : IAICompletionContextBuilderHandler
{
    public Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        if (context.Resource is not ChatInteraction interaction)
        {
            return Task.CompletedTask;
        }

        context.Context.ConnectionName = interaction.ConnectionName;
        context.Context.DeploymentId = interaction.DeploymentId;
        context.Context.SystemMessage = interaction.SystemMessage;
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

        if (interaction.TryGet<ChatInteractionDataSourceMetadata>(out var dataSourceMetadata))
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

        return Task.CompletedTask;
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
        => Task.CompletedTask;
}
