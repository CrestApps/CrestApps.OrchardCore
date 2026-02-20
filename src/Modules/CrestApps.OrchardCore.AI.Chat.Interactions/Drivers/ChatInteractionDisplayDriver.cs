using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Orchestration;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

public sealed class ChatInteractionDisplayDriver : DisplayDriver<ChatInteraction>
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IChatInteractionPromptStore _promptStore;
    private readonly OrchestratorOptions _orchestratorOptions;

    public ChatInteractionDisplayDriver(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IChatInteractionPromptStore promptStore,
        IOptions<OrchestratorOptions> orchestratorOptions)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _promptStore = promptStore;
        _orchestratorOptions = orchestratorOptions.Value;
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

        // Title is placed first in Settings tab.
        var titleResult = Initialize<EditChatInteractionTitleViewModel>("ChatInteractionTitle_Edit", model =>
        {
            model.ItemId = interaction.ItemId;
            model.Title = interaction.Title;
            model.IsNew = context.IsNew;
        }).Location("Parameters:1#Settings;1");

        // Orchestrator selection comes after title.
        var orchestratorResult = Initialize<OrchestratorViewModel>("OrchestratorSelection_Edit", model =>
        {
            // Populate orchestrator selection when multiple orchestrators are registered.
            var orchestrators = _orchestratorOptions.GetOrchestratorDescriptors();
            if (orchestrators.Count > 1)
            {
                model.OrchestratorName = interaction.OrchestratorName;
                model.Orchestrators = orchestrators
                    .Select(x => new SelectListItem(x.Value.Title ?? x.Key, x.Key))
                    .ToArray();
            }
        }).Location("Parameters:2#Settings;1");

        // Connection/Deployment at position 3 - handled by ChatInteractionConnectionDisplayDriver.
        // Copilot config at position 4 - handled by ChatInteractionCopilotDisplayDriver.
        // Data source at position 5 - handled by ChatInteractionDataSourceDisplayDriver.

        // General parameters come last in the Settings tab.
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
            model.McpConnectionIds = interaction.McpConnectionIds?.ToArray();
            model.IsNew = context.IsNew;
        }).Location("Parameters:8#Settings;1");

        return Combine(headerResult, contentResult, titleResult, orchestratorResult, parametersResult);
    }
}
