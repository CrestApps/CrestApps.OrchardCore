using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
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
    private readonly IChatInteractionPromptStore _promptStore;

    public ChatInteractionDisplayDriver(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IChatInteractionPromptStore promptStore)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _promptStore = promptStore;
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

    public override async Task<IDisplayResult> EditAsync(ChatInteraction interaction, BuildEditorContext context)
    {
        var prompts = await _promptStore.GetPromptsAsync(interaction.ItemId);

        var headerResult = Initialize<ChatInteractionCapsuleViewModel>("ChatInteractionHeader", model =>
        {
            model.Interaction = interaction;
            model.Prompts = prompts;
            model.IsNew = context.IsNew;
        }).Location("Header");

        var contentResult = Initialize<ChatInteractionCapsuleViewModel>("ChatInteractionChat", model =>
        {
            model.Interaction = interaction;
            model.Prompts = prompts;
            model.IsNew = context.IsNew;
        }).Location("Content");

        // Title is placed first in Settings tab (position 1)
        var titleResult = Initialize<EditChatInteractionTitleViewModel>("ChatInteractionTitle_Edit", model =>
        {
            model.ItemId = interaction.ItemId;
            model.Title = interaction.Title;
            model.IsNew = context.IsNew;
        }).Location("Parameters:1#Settings:1");

        // Connection/Deployment comes after title (position 2) - handled by ChatInteractionConnectionDisplayDriver

        // Parameters come after connection (position 3)
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
        }).Location("Parameters:4#Settings:5");

        return Combine(headerResult, contentResult, titleResult, parametersResult);
    }
}
