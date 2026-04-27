using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Memory.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Memory.Drivers;

/// <summary>
/// Display driver for the chat interaction memory settings shape.
/// </summary>
public sealed class ChatInteractionMemorySettingsDisplayDriver : SiteDisplayDriver<MemoryMetadata>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISiteService _siteService;
    private readonly IShellReleaseManager _shellReleaseManager;

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatInteractionMemorySettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The http context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="shellReleaseManager">The shell release manager.</param>
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
