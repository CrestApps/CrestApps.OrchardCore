using CrestApps.OrchardCore.AI.Core;
using CrestApps.AI;
using CrestApps.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Drivers;

public sealed class GeneralAISettingsDisplayDriver : SiteDisplayDriver<GeneralAISettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly DefaultAIOptions _defaultAIOptions;

    internal readonly IStringLocalizer T;

    protected override string SettingsGroupId => AIConstants.AISettingsGroupId;

    public GeneralAISettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IOptions<DefaultAIOptions> defaultAIOptions,
        IStringLocalizer<GeneralAISettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _defaultAIOptions = defaultAIOptions.Value;
        T = stringLocalizer;
    }

    public override IDisplayResult Edit(ISite site, GeneralAISettings settings, BuildEditorContext context)
    {
        return Initialize<GeneralAISettingsViewModel>("GeneralAISettings_Edit", model =>
        {
            model.EnablePreemptiveMemoryRetrieval = settings.EnablePreemptiveMemoryRetrieval;
            model.OverrideMaximumIterationsPerRequest = settings.OverrideMaximumIterationsPerRequest;
            model.MaximumIterationsPerRequest = settings.OverrideMaximumIterationsPerRequest
                ? settings.MaximumIterationsPerRequest
                : _defaultAIOptions.MaximumIterationsPerRequest;
            model.AppSettingsMaximumIterationsPerRequest = _defaultAIOptions.MaximumIterationsPerRequest;
            model.AbsoluteMaximumIterationsPerRequest = _defaultAIOptions.AbsoluteMaximumIterationsPerRequest;
            model.OverrideEnableDistributedCaching = settings.OverrideEnableDistributedCaching;
            model.EnableDistributedCaching = settings.OverrideEnableDistributedCaching
                ? settings.EnableDistributedCaching
                : _defaultAIOptions.EnableDistributedCaching;
            model.AppSettingsEnableDistributedCaching = _defaultAIOptions.EnableDistributedCaching;
            model.OverrideEnableOpenTelemetry = settings.OverrideEnableOpenTelemetry;
            model.EnableOpenTelemetry = settings.OverrideEnableOpenTelemetry
                ? settings.EnableOpenTelemetry
                : _defaultAIOptions.EnableOpenTelemetry;
            model.AppSettingsEnableOpenTelemetry = _defaultAIOptions.EnableOpenTelemetry;
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

        if (model.OverrideMaximumIterationsPerRequest)
        {
            if (model.MaximumIterationsPerRequest < 1)
            {
                context.Updater.ModelState.AddModelError($"{Prefix}.{nameof(model.MaximumIterationsPerRequest)}", T["Maximum iterations per request must be at least {0}.", 1]);
            }

            if (model.MaximumIterationsPerRequest > _defaultAIOptions.AbsoluteMaximumIterationsPerRequest)
            {
                context.Updater.ModelState.AddModelError($"{Prefix}.{nameof(model.MaximumIterationsPerRequest)}", T["Maximum iterations per request cannot exceed the absolute maximum of {0}.", _defaultAIOptions.AbsoluteMaximumIterationsPerRequest]);
            }
        }

        if (!context.Updater.ModelState.IsValid)
        {
            return Edit(site, settings, context);
        }

        settings.EnablePreemptiveMemoryRetrieval = model.EnablePreemptiveMemoryRetrieval;
        settings.OverrideMaximumIterationsPerRequest = model.OverrideMaximumIterationsPerRequest;
        settings.MaximumIterationsPerRequest = Math.Min(model.MaximumIterationsPerRequest, _defaultAIOptions.AbsoluteMaximumIterationsPerRequest);
        settings.OverrideEnableDistributedCaching = model.OverrideEnableDistributedCaching;
        settings.EnableDistributedCaching = model.EnableDistributedCaching;
        settings.OverrideEnableOpenTelemetry = model.OverrideEnableOpenTelemetry;
        settings.EnableOpenTelemetry = model.EnableOpenTelemetry;

        return Edit(site, settings, context);
    }
}
