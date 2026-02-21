using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Drivers;

public sealed class DefaultOrchestratorSettingsDisplayDriver : SiteDisplayDriver<DefaultOrchestratorSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    public DefaultOrchestratorSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    public override IDisplayResult Edit(ISite site, DefaultOrchestratorSettings settings, BuildEditorContext context)
    {
        return Initialize<DefaultOrchestratorSettingsViewModel>("DefaultOrchestratorSettings_Edit", model =>
        {
            model.EnablePreemptiveRag = settings.EnablePreemptiveRag;
        }).Location("Content:5%Default Orchestrator;1")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, DefaultOrchestratorSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles))
        {
            return null;
        }

        var model = new DefaultOrchestratorSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.EnablePreemptiveRag = model.EnablePreemptiveRag;

        return Edit(site, settings, context);
    }
}
