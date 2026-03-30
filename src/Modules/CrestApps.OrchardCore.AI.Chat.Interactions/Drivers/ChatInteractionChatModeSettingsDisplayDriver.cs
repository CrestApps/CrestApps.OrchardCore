using CrestApps.OrchardCore.AI.Chat.Interactions.Settings;
using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

public sealed class ChatInteractionChatModeSettingsDisplayDriver : SiteDisplayDriver<ChatInteractionChatModeSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly IStringLocalizer S;

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    public ChatInteractionChatModeSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IAIDeploymentManager deploymentManager,
        IStringLocalizer<ChatInteractionChatModeSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _deploymentManager = deploymentManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ISite site, ChatInteractionChatModeSettings settings, BuildEditorContext context)
    {
        return Initialize<ChatInteractionChatModeSettingsViewModel>("ChatInteractionChatModeSettings_Edit", async model =>
        {
            model.ChatMode = settings.ChatMode;
            model.AvailableModes = await GetAvailableModesAsync();
        }).Location("Content:4.5%Chat Interactions;1")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.EditChatInteractions));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, ChatInteractionChatModeSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.EditChatInteractions))
        {
            return null;
        }

        var model = new ChatInteractionChatModeSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.ChatMode = model.ChatMode;

        return Edit(site, settings, context);
    }

    private async Task<IEnumerable<SelectListItem>> GetAvailableModesAsync()
    {
        var hasSpeechToText = await _deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.SpeechToText) != null;
        var hasTextToSpeech = await _deploymentManager.ResolveOrDefaultAsync(AIDeploymentType.TextToSpeech) != null;

        var modes = new List<SelectListItem>
        {
            new(S["Text input"], nameof(ChatMode.TextInput)),
        };

        if (hasSpeechToText)
        {
            modes.Add(new SelectListItem(S["Audio input"], nameof(ChatMode.AudioInput)));
        }

        if (hasSpeechToText && hasTextToSpeech)
        {
            modes.Add(new SelectListItem(S["Conversation"], nameof(ChatMode.Conversation)));
        }

        return modes;
    }
}
