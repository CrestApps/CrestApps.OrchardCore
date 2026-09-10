using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

/// <summary>
/// Display driver that renders and persists the tenant-scoped hosted secure data capture policy on the Contact
/// Center site settings screen.
/// </summary>
public sealed class SecureCaptureSettingsDisplayDriver
    : SiteDisplayDriver<SecureCaptureSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    /// <inheritdoc/>
    protected override string SettingsGroupId
        => ContactCenterConstants.Settings.GroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecureCaptureSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SecureCaptureSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<SecureCaptureSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(
        ISite site,
        SecureCaptureSettings settings,
        BuildEditorContext context)
    {
        return Initialize<SecureCaptureSettingsViewModel>(
            "SecureCaptureSettings_Edit",
            model =>
            {
                model.Enabled = settings.Enabled;
                model.LinkTimeToLiveSeconds = settings.LinkTimeToLiveSeconds;
                model.PauseRecordingDuringCapture = settings.PauseRecordingDuringCapture;
            })
            .Location("Content:5#Secure Data Capture")
            .OnGroup(SettingsGroupId)
            .RenderWhen(() => _authorizationService.AuthorizeAsync(
                _httpContextAccessor.HttpContext?.User,
                ContactCenterPermissions.ManageContactCenter));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(
        ISite site,
        SecureCaptureSettings settings,
        UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(
                _httpContextAccessor.HttpContext?.User,
                ContactCenterPermissions.ManageContactCenter))
        {
            return null;
        }

        var model = new SecureCaptureSettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.Enabled = model.Enabled;
        settings.LinkTimeToLiveSeconds = Math.Clamp(
            model.LinkTimeToLiveSeconds,
            SecureCaptureSettings.MinLinkTimeToLiveSeconds,
            SecureCaptureSettings.MaxLinkTimeToLiveSeconds);
        settings.PauseRecordingDuringCapture = model.PauseRecordingDuringCapture;

        return Edit(site, settings, context);
    }
}
