using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Display driver for managing the general AI settings on the site settings page.
/// </summary>
public sealed class GeneralAISettingsDisplayDriver : SiteDisplayDriver<GeneralAISettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly DefaultAIOptions _defaultAIOptions;
    private readonly IShellReleaseManager _shellReleaseManager;

    internal readonly IStringLocalizer S;

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneralAISettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="shellReleaseManager">The shell release manager.</param>
    /// <param name="defaultAIOptions">The default AI options.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public GeneralAISettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IShellReleaseManager shellReleaseManager,
        IOptions<DefaultAIOptions> defaultAIOptions,
        IStringLocalizer<GeneralAISettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _shellReleaseManager = shellReleaseManager;
        _defaultAIOptions = defaultAIOptions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ISite site, GeneralAISettings settings, BuildEditorContext context)
    {
        context.AddTenantReloadWarningWrapper();

        return Initialize<GeneralAISettingsViewModel>("GeneralAISettings_Edit", model =>
        {
            model.EnableAIUsageTracking = settings.EnableAIUsageTracking;
            model.EnablePreemptiveMemoryRetrieval = settings.EnablePreemptiveMemoryRetrieval;
            model.MaximumIterationsPerRequest = settings.MaximumIterationsPerRequest;
            model.AbsoluteMaximumIterationsPerRequest = _defaultAIOptions.AbsoluteMaximumIterationsPerRequest;
            model.EnableDistributedCaching = settings.EnableDistributedCaching;
            model.EnableOpenTelemetry = settings.EnableOpenTelemetry;
        }).Location("Content:1%General;1")
        .OnGroup(SettingsGroupId)
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles));
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, GeneralAISettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles))
        {
            return null;
        }

        var model = new GeneralAISettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);
        var maximumIterationsPerRequest = Math.Min(model.MaximumIterationsPerRequest, _defaultAIOptions.AbsoluteMaximumIterationsPerRequest);
        var settingsChanged =
            settings.EnableAIUsageTracking != model.EnableAIUsageTracking ||
            settings.EnablePreemptiveMemoryRetrieval != model.EnablePreemptiveMemoryRetrieval ||
            settings.MaximumIterationsPerRequest != maximumIterationsPerRequest ||
            settings.EnableDistributedCaching != model.EnableDistributedCaching ||
            settings.EnableOpenTelemetry != model.EnableOpenTelemetry;

        if (model.MaximumIterationsPerRequest < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaximumIterationsPerRequest), S["Maximum iterations per request must be at least {0}.", 1]);
        }

        if (model.MaximumIterationsPerRequest > _defaultAIOptions.AbsoluteMaximumIterationsPerRequest)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaximumIterationsPerRequest), S["Maximum iterations per request cannot exceed the absolute maximum of {0}.", _defaultAIOptions.AbsoluteMaximumIterationsPerRequest]);
        }

        if (!context.Updater.ModelState.IsValid)
        {
            return Edit(site, settings, context);
        }

        settings.EnableAIUsageTracking = model.EnableAIUsageTracking;
        settings.EnablePreemptiveMemoryRetrieval = model.EnablePreemptiveMemoryRetrieval;
        settings.MaximumIterationsPerRequest = maximumIterationsPerRequest;
        settings.EnableDistributedCaching = model.EnableDistributedCaching;
        settings.EnableOpenTelemetry = model.EnableOpenTelemetry;

        if (settingsChanged)
        {
            _shellReleaseManager.RequestRelease();
        }

        return Edit(site, settings, context);
    }
}
