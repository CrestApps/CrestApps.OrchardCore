using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

public sealed class ChatInteractionDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ChatInteractionDisplayDriver(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override IDisplayResult Display(ChatInteraction interaction, BuildDisplayContext context)
    {
        return Combine(
            View("ChatInteraction_Fields_SummaryAdmin", interaction).Location("Content:1"),
            View("ChatInteraction_Buttons_SummaryAdmin", interaction).Location("Actions:5"),
            View("ChatInteraction_DefaultTags_SummaryAdmin", interaction).Location("Tags:5"),
            View("ChatInteraction_DefaultMeta_SummaryAdmin", interaction).Location("Meta:5"),
            View("ChatInteraction_ActionsMenu_SummaryAdmin", interaction)
                .Location("ActionsMenu:10")
                .RenderWhen(async () => await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.DeleteChatInteraction, interaction))
        );
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
            model.ItemId = interaction.ItemId;
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
