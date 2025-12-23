using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

public sealed class ChatInteractionDisplayDriver : DisplayDriver<ChatInteraction>
{
    public override IDisplayResult Display(ChatInteraction interaction, BuildDisplayContext context)
    {
        return Initialize<DisplayChatInteractionViewModel>("ChatInteractionListItem", model =>
        {
            model.Interaction = interaction;
        }).Location("SummaryAdmin", "Content");
    }

    public override Task<IDisplayResult> EditAsync(ChatInteraction interaction, BuildEditorContext context)
    {
        var headerResult = Initialize<ChatInteractionCapsuleViewModel>("ChatInteractionHeader", model =>
        {
            model.Interaction = interaction;
            model.IsNew = context.IsNew;
        }).Location("Header");

        var contentResult = Initialize<ChatInteractionCapsuleViewModel>("ChatInteractionChat", model =>
        {
            model.Interaction = interaction;
            model.IsNew = context.IsNew;
        }).Location("Content");

        var parametersResult = Initialize<EditChatInteractionViewModel>("ChatInteractionParameters_Edit", model =>
        {
            model.InteractionId = interaction.InteractionId;
            model.Title = interaction.Title;
            model.DeploymentId = interaction.DeploymentId;
            model.ConnectionName = interaction.ConnectionName;
            model.SystemMessage = interaction.SystemMessage;
            model.Temperature = interaction.Temperature;
            model.TopP = interaction.TopP;
            model.FrequencyPenalty = interaction.FrequencyPenalty;
            model.PresencePenalty = interaction.PresencePenalty;
            model.MaxTokens = interaction.MaxTokens;
            model.PastMessagesCount = interaction.PastMessagesCount;
            model.ToolNames = interaction.ToolNames?.ToArray();
            model.ToolInstanceIds = interaction.ToolInstanceIds?.ToArray();
            model.McpConnectionIds = interaction.McpConnectionIds?.ToArray();
            model.IsNew = context.IsNew;
        }).Location("Parameters:1#Settings:1");

        return CombineAsync(headerResult, contentResult, parametersResult);
    }
}
