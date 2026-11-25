using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Display driver for the default orchestrator settings shape.
/// </summary>
public sealed class DefaultOrchestratorSettingsDisplayDriver : SiteDisplayDriver<DefaultOrchestratorSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultOrchestratorSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The http context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DefaultOrchestratorSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<DefaultOrchestratorSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ISite site, DefaultOrchestratorSettings settings, BuildEditorContext context)
    {
        return Initialize<DefaultOrchestratorSettingsViewModel>("DefaultOrchestratorSettings_Edit", model =>
        {
            model.EnablePreemptiveRag = settings.EnablePreemptiveRag;
        }).Location(l => l.Zone("Content", "3").Card(S["Default Orchestrator"], "1"))
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
