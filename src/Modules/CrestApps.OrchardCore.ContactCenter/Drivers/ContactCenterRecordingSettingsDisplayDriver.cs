using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

/// <summary>
/// Display driver that renders and persists the tenant-scoped recording governance policy on the Contact Center
/// site settings screen.
/// </summary>
public sealed class ContactCenterRecordingSettingsDisplayDriver
    : SiteDisplayDriver<ContactCenterRecordingSettings>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    /// <inheritdoc/>
    protected override string SettingsGroupId
        => ContactCenterConstants.Settings.GroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterRecordingSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterRecordingSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<ContactCenterRecordingSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(
        ISite site,
        ContactCenterRecordingSettings settings,
        BuildEditorContext context)
    {
        return Initialize<ContactCenterRecordingSettingsViewModel>(
            "ContactCenterRecordingSettings_Edit",
            model =>
            {
                model.RecordingEnabled = settings.RecordingEnabled;
                model.ConsentModel = settings.ConsentModel;
                model.RequireExplicitConsent = settings.RequireExplicitConsent;
                model.RetentionDays = settings.RetentionDays;
                model.LegalHoldByDefault = settings.LegalHoldByDefault;
            })
            .Location("Content:4#Recording governance")
            .OnGroup(SettingsGroupId)
            .RenderWhen(() => _authorizationService.AuthorizeAsync(
                _httpContextAccessor.HttpContext?.User,
                ContactCenterPermissions.ManageContactCenter));
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(
        ISite site,
        ContactCenterRecordingSettings settings,
        UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(
                _httpContextAccessor.HttpContext?.User,
                ContactCenterPermissions.ManageContactCenter))
        {
            return null;
        }

        var model = new ContactCenterRecordingSettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!Enum.IsDefined(model.ConsentModel))
        {
            context.Updater.ModelState.AddModelError(
                Prefix,
                nameof(model.ConsentModel),
                S["Select a valid consent model."]);
        }

        if (context.Updater.ModelState.IsValid)
        {
            settings.RecordingEnabled = model.RecordingEnabled;
            settings.ConsentModel = model.ConsentModel;
            settings.RequireExplicitConsent = model.RequireExplicitConsent;
            settings.RetentionDays = Math.Clamp(model.RetentionDays, 0, ContactCenterRecordingSettings.MaxRetentionDays);
            settings.LegalHoldByDefault = model.LegalHoldByDefault;
        }

        return Edit(site, settings, context);
    }
}
