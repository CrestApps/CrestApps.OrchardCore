using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Memory.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Indexing;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Memory.Drivers;

public sealed class AIMemorySettingsDisplayDriver : SiteDisplayDriver<AIMemorySettings>
{
    private readonly IIndexProfileStore _indexProfileStore;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShellReleaseManager _shellReleaseManager;

    internal readonly IStringLocalizer S;

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    public AIMemorySettingsDisplayDriver(
        IIndexProfileStore indexProfileStore,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IShellReleaseManager shellReleaseManager,
        IStringLocalizer<AIMemorySettingsDisplayDriver> stringLocalizer)
    {
        _indexProfileStore = indexProfileStore;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _shellReleaseManager = shellReleaseManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ISite site, AIMemorySettings settings, BuildEditorContext context)
    {
        context.AddTenantReloadWarningWrapper();

        return Initialize<AIMemorySettingsViewModel>("AIMemorySettings_Edit", async model =>
        {
            model.IndexProfileName = settings.IndexProfileName;
            model.TopN = settings.TopN;
            model.IndexProfiles = (await _indexProfileStore.GetByTypeAsync(MemoryConstants.IndexingTaskType))
            .Select(x => new SelectListItem(x.Name, x.Name));
        }).Location("Content:5.1%Memory;1")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, AIMemorySettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles))
        {
            return null;
        }

        var model = new AIMemorySettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var indexProfileName = string.IsNullOrWhiteSpace(model.IndexProfileName)
        ? null
        : model.IndexProfileName;
        var topN = Math.Clamp(model.TopN, 1, 20);
        var settingsChanged =
            !string.Equals(settings.IndexProfileName, indexProfileName, StringComparison.Ordinal) ||
            settings.TopN != topN;

        settings.IndexProfileName = indexProfileName;

        if (!string.IsNullOrWhiteSpace(settings.IndexProfileName))
        {
            var indexProfile = await _indexProfileStore.FindByNameAsync(settings.IndexProfileName);

            if (indexProfile is null || !string.Equals(indexProfile.Type, MemoryConstants.IndexingTaskType, StringComparison.OrdinalIgnoreCase))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.IndexProfileName), S["Invalid index profile."]);
            }
        }

        settings.TopN = topN;

        if (!context.Updater.ModelState.IsValid)
        {
            return Edit(site, settings, context);
        }

        if (settingsChanged)
        {
            _shellReleaseManager.RequestRelease();
        }

        return Edit(site, settings, context);
    }
}
