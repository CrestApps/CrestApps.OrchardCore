using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Memory.Models;
using CrestApps.OrchardCore.AI.Memory.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Memory.Drivers;

public sealed class ChatInteractionMemorySettingsDisplayDriver : SiteDisplayDriver<MemoryMetadata>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISiteService _siteService;
    private readonly IShellReleaseManager _shellReleaseManager;

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    public ChatInteractionMemorySettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        ISiteService siteService,
        IShellReleaseManager shellReleaseManager)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _siteService = siteService;
        _shellReleaseManager = shellReleaseManager;
    }

    public override IDisplayResult Edit(ISite site, MemoryMetadata settings, BuildEditorContext context)
    {
        context.AddTenantReloadWarningWrapper();

        return Initialize<ChatInteractionMemorySettingsViewModel>("ChatInteractionMemorySettings_Edit", async model =>
        {
            model.EnableUserMemory = settings.EnableUserMemory ?? true;
            model.HasIndexProfile = !string.IsNullOrEmpty((await _siteService.GetSettingsAsync<AIMemorySettings>()).IndexProfileName);
        }).Location("Content:4.6%Chat Interactions;2")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageChatInteractionSettings));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, MemoryMetadata settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageChatInteractionSettings))
        {
            return null;
        }

        var model = new ChatInteractionMemorySettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var settingsChanged = (settings.EnableUserMemory ?? true) != model.EnableUserMemory;
        settings.EnableUserMemory = model.EnableUserMemory;

        if (settingsChanged)
        {
            _shellReleaseManager.RequestRelease();
        }

        return Edit(site, settings, context);
    }
}
