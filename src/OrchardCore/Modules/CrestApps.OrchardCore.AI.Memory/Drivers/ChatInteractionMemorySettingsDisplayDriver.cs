using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Memory.Models;
using CrestApps.OrchardCore.AI.Memory.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Memory.Drivers;

public sealed class ChatInteractionMemorySettingsDisplayDriver : SiteDisplayDriver<ChatInteractionMemorySettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISiteService _siteService;
    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;
    public ChatInteractionMemorySettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        ISiteService siteService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _siteService = siteService;
    }

    public override IDisplayResult Edit(ISite site, ChatInteractionMemorySettings settings, BuildEditorContext context)
    {
        return Initialize<ChatInteractionMemorySettingsViewModel>("ChatInteractionMemorySettings_Edit", async model =>
        {
            model.EnableUserMemory = settings.EnableUserMemory;
            model.HasIndexProfile = !string.IsNullOrEmpty((await _siteService.GetSettingsAsync<AIMemorySettings>()).IndexProfileName);
        }).Location("Content:4.6%Chat Interactions;2")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageChatInteractionSettings));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, ChatInteractionMemorySettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageChatInteractionSettings))
        {
            return null;
        }

        var model = new ChatInteractionMemorySettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);
        settings.EnableUserMemory = model.EnableUserMemory;

        return Edit(site, settings, context);
    }
}
