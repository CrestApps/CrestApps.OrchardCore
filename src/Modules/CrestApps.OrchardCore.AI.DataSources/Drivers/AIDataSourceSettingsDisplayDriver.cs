using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.DataSources.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.DataSources.Drivers;

public sealed class AIDataSourceSettingsDisplayDriver : SiteDisplayDriver<AIDataSourceSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    public AIDataSourceSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    public override IDisplayResult Edit(ISite site, AIDataSourceSettings settings, BuildEditorContext context)
    {
        return Initialize<AIDataSourceSettingsViewModel>("AIDataSourceSettings_Edit", model =>
        {
            model.EnableEarlyRag = settings.EnableEarlyRag;
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

        settings.EnableEarlyRag = model.EnableEarlyRag;
        settings.DefaultStrictness = Math.Clamp(model.DefaultStrictness, 1, 5);
        settings.DefaultTopNDocuments = Math.Clamp(model.DefaultTopNDocuments, 3, 20);

        return Edit(site, settings, context);
    }
}
