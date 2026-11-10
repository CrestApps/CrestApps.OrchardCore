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
using OrchardCore.Environment.Options;
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
    private readonly IOptionsUpdateNotifier _optionsUpdateNotifier;

    internal readonly IStringLocalizer S;

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneralAISettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="optionsUpdateNotifier">The options update notifier.</param>
    /// <param name="defaultAIOptions">The default AI options.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public GeneralAISettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IOptionsUpdateNotifier optionsUpdateNotifier,
        IOptions<DefaultAIOptions> defaultAIOptions,
        IStringLocalizer<GeneralAISettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _optionsUpdateNotifier = optionsUpdateNotifier;
        _defaultAIOptions = defaultAIOptions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ISite site, GeneralAISettings settings, BuildEditorContext context)
    {
        return Initialize<GeneralAISettingsViewModel>("GeneralAISettings_Edit", model =>
        {
            model.EnablePreemptiveMemoryRetrieval = settings.EnablePreemptiveMemoryRetrieval;
            // A setting the site has not overridden shows the value configuration gives it, the one in effect.
            model.MaximumIterationsPerRequest = settings.OverrideMaximumIterationsPerRequest
                ? settings.MaximumIterationsPerRequest
                : _defaultAIOptions.MaximumIterationsPerRequest;
            model.AbsoluteMaximumIterationsPerRequest = _defaultAIOptions.AbsoluteMaximumIterationsPerRequest;
            model.EnableDistributedCaching = settings.OverrideEnableDistributedCaching
                ? settings.EnableDistributedCaching
                : _defaultAIOptions.EnableDistributedCaching;
            model.EnableOpenTelemetry = settings.OverrideEnableOpenTelemetry
                ? settings.EnableOpenTelemetry
                : _defaultAIOptions.EnableOpenTelemetry;
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
            settings.EnablePreemptiveMemoryRetrieval != model.EnablePreemptiveMemoryRetrieval ||
            GetMaximumIterationsPerRequest(settings) != maximumIterationsPerRequest ||
            GetEnableDistributedCaching(settings) != model.EnableDistributedCaching ||
            GetEnableOpenTelemetry(settings) != model.EnableOpenTelemetry;

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

        // A saved value overrides configuration only when it differs from it; without the override flags, the saved
        // values were ignored. A value equal to configuration keeps following configuration.
        settings.EnablePreemptiveMemoryRetrieval = model.EnablePreemptiveMemoryRetrieval;
        settings.MaximumIterationsPerRequest = maximumIterationsPerRequest;
        settings.OverrideMaximumIterationsPerRequest = maximumIterationsPerRequest != _defaultAIOptions.MaximumIterationsPerRequest;
        settings.EnableDistributedCaching = model.EnableDistributedCaching;
        settings.OverrideEnableDistributedCaching = model.EnableDistributedCaching != _defaultAIOptions.EnableDistributedCaching;
        settings.EnableOpenTelemetry = model.EnableOpenTelemetry;
        settings.OverrideEnableOpenTelemetry = model.EnableOpenTelemetry != _defaultAIOptions.EnableOpenTelemetry;

        if (settingsChanged)
        {
            _optionsUpdateNotifier.RequestUpdate<GeneralAIOptions>();
        }

        return Edit(site, settings, context);
    }

    private int GetMaximumIterationsPerRequest(GeneralAISettings settings)
        => settings.OverrideMaximumIterationsPerRequest ? settings.MaximumIterationsPerRequest : _defaultAIOptions.MaximumIterationsPerRequest;

    private bool GetEnableDistributedCaching(GeneralAISettings settings)
        => settings.OverrideEnableDistributedCaching ? settings.EnableDistributedCaching : _defaultAIOptions.EnableDistributedCaching;

    private bool GetEnableOpenTelemetry(GeneralAISettings settings)
        => settings.OverrideEnableOpenTelemetry ? settings.EnableOpenTelemetry : _defaultAIOptions.EnableOpenTelemetry;
}
