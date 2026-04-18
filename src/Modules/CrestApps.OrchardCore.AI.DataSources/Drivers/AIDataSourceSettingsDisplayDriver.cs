using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.DataSources.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.DataSources.Drivers;

public sealed class AIDataSourceSettingsDisplayDriver : SiteDisplayDriver<AIDataSourceSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IShellReleaseManager _shellReleaseManager;

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    public AIDataSourceSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IShellReleaseManager shellReleaseManager)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _shellReleaseManager = shellReleaseManager;
    }

    public override IDisplayResult Edit(ISite site, AIDataSourceSettings settings, BuildEditorContext context)
    {
        context.AddTenantReloadWarningWrapper();

        return Initialize<AIDataSourceSettingsViewModel>("AIDataSourceSettings_Edit", model =>
        {
            model.DefaultStrictness = settings.DefaultStrictness;
            model.DefaultTopNDocuments = settings.DefaultTopNDocuments;
        }).Location("Content:5%Data Sources;1")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, AIDataSourceSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles))
        {
            return null;
        }

        var model = new AIDataSourceSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var defaultStrictness = Math.Clamp(model.DefaultStrictness, 1, 5);
        var defaultTopNDocuments = Math.Clamp(model.DefaultTopNDocuments, 3, 20);
        var settingsChanged =
            settings.DefaultStrictness != defaultStrictness ||
            settings.DefaultTopNDocuments != defaultTopNDocuments;

        settings.DefaultStrictness = defaultStrictness;
        settings.DefaultTopNDocuments = defaultTopNDocuments;

        if (settingsChanged)
        {
            _shellReleaseManager.RequestRelease();
        }

        return Edit(site, settings, context);
    }
}
