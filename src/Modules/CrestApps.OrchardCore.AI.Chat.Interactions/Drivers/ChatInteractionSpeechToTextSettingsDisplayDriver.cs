using CrestApps.OrchardCore.AI.Chat.Interactions.Settings;
using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Drivers;

public sealed class ChatInteractionSpeechToTextSettingsDisplayDriver : SiteDisplayDriver<ChatInteractionSpeechToTextSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    public ChatInteractionSpeechToTextSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    public override IDisplayResult Edit(ISite site, ChatInteractionSpeechToTextSettings settings, BuildEditorContext context)
    {
        return Initialize<ChatInteractionSpeechToTextSettingsViewModel>("ChatInteractionSpeechToTextSettings_Edit", model =>
        {
            model.EnableSpeechToText = settings.EnableSpeechToText;
        }).Location("Content:10%Chat Interactions;1")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.EditChatInteractions));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, ChatInteractionSpeechToTextSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.EditChatInteractions))
        {
            return null;
        }

        var model = new ChatInteractionSpeechToTextSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.EnableSpeechToText = model.EnableSpeechToText;

        return Edit(site, settings, context);
    }
}
