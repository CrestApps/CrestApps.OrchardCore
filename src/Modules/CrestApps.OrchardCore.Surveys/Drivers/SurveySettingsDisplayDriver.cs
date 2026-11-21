using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Surveys.Drivers;

/// <summary>
/// Edits the site settings of Surveys.
/// </summary>
public sealed class SurveySettingsDisplayDriver : SiteDisplayDriver<SurveySettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveySettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public SurveySettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc />
    protected override string SettingsGroupId
        => SurveysConstants.SettingsGroupId;

    /// <inheritdoc />
    public override IDisplayResult Edit(ISite site, SurveySettings settings, BuildEditorContext context)
        => Initialize<SurveySettingsViewModel>("SurveySettings_Edit", model =>
        {
            model.DefaultRetentionDays = settings.DefaultRetentionDays;
            model.DefaultIncompleteExpiryHours = settings.DefaultIncompleteExpiryHours;
            model.MaxUploadMegabytes = settings.MaxUploadMegabytes;
            model.AllowedRedirectHosts = string.Join(Environment.NewLine, settings.AllowedRedirectHosts ?? []);
            model.DefaultMinimumGroupSize = settings.DefaultMinimumGroupSize;
        }).Location("Content:1")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, SurveysPermissions.ManageSurveySettings))
        .OnGroup(SettingsGroupId);

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(ISite site, SurveySettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, SurveysPermissions.ManageSurveySettings))
        {
            return null;
        }

        var model = new SurveySettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.DefaultRetentionDays = model.DefaultRetentionDays is > 0 ? model.DefaultRetentionDays : null;
        settings.DefaultIncompleteExpiryHours = Math.Max(1, model.DefaultIncompleteExpiryHours);
        settings.MaxUploadMegabytes = Math.Clamp(model.MaxUploadMegabytes, 1, 1024);
        settings.DefaultMinimumGroupSize = Math.Max(0, model.DefaultMinimumGroupSize);
        settings.AllowedRedirectHosts = (model.AllowedRedirectHosts ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(host => host.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return Edit(site, settings, context);
    }
}
