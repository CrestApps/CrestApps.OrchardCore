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
        // Every card edits the same model under the same prefix, so UpdateAsync still binds it once.
        void Populate(ContactCenterRecordingSettingsViewModel model)
        {
            model.RecordAllCalls = settings.RecordAllCalls;
            model.ConsentModel = settings.ConsentModel;
            model.RequireExplicitConsent = settings.RequireExplicitConsent;
            model.EnableRecordingDisclosure = settings.EnableRecordingDisclosure;
            model.RecordingDisclosureText = string.IsNullOrWhiteSpace(settings.RecordingDisclosureText)
                ? S["This call may be recorded for quality assurance and training purposes."].Value
                : settings.RecordingDisclosureText;
            model.DiscloseOnInboundCalls = settings.DiscloseOnInboundCalls;
            model.DiscloseOnAIVoiceCalls = settings.DiscloseOnAIVoiceCalls;
            model.PromptAgentsToDisclose = settings.PromptAgentsToDisclose;
            model.RetentionDays = settings.RetentionDays;
            model.LegalHoldByDefault = settings.LegalHoldByDefault;
            model.AllowAgentSecurePause = settings.AllowAgentSecurePause;
            model.MaxSecurePauseSeconds = settings.MaxSecurePauseSeconds;
            model.RequirePauseReason = settings.RequirePauseReason;
        }

        return Combine(
            Initialize<ContactCenterRecordingSettingsViewModel>("ContactCenterRecordingSettings_Edit", Populate)
                .Location(l => l.Zone("Content", "4").Tab(S["Recording Governance"], "4").Card(S["Recording"], "1"))
                .OnGroup(SettingsGroupId)
                .RenderWhen(() => _authorizationService.AuthorizeAsync(
                    _httpContextAccessor.HttpContext?.User,
                    ContactCenterPermissions.ManageContactCenter)),
            Initialize<ContactCenterRecordingSettingsViewModel>("ContactCenterRecordingSettingsDisclosure_Edit", Populate)
                .Location(l => l.Zone("Content", "4").Tab(S["Recording Governance"], "4").Card(S["Recording Disclosure"], "2"))
                .OnGroup(SettingsGroupId)
                .RenderWhen(() => _authorizationService.AuthorizeAsync(
                    _httpContextAccessor.HttpContext?.User,
                    ContactCenterPermissions.ManageContactCenter)),
            Initialize<ContactCenterRecordingSettingsViewModel>("ContactCenterRecordingSettingsRetention_Edit", Populate)
                .Location(l => l.Zone("Content", "4").Tab(S["Recording Governance"], "4").Card(S["Retention"], "3"))
                .OnGroup(SettingsGroupId)
                .RenderWhen(() => _authorizationService.AuthorizeAsync(
                    _httpContextAccessor.HttpContext?.User,
                    ContactCenterPermissions.ManageContactCenter)),
            Initialize<ContactCenterRecordingSettingsViewModel>("ContactCenterRecordingSettingsSecurePause_Edit", Populate)
                .Location(l => l.Zone("Content", "4").Tab(S["Recording Governance"], "4").Card(S["Agent Secure Pause"], "4"))
                .OnGroup(SettingsGroupId)
                .RenderWhen(() => _authorizationService.AuthorizeAsync(
                    _httpContextAccessor.HttpContext?.User,
                    ContactCenterPermissions.ManageContactCenter)));
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

        var disclosureText = model.RecordingDisclosureText?.Trim();

        if (model.EnableRecordingDisclosure && string.IsNullOrEmpty(disclosureText))
        {
            context.Updater.ModelState.AddModelError(
                Prefix,
                nameof(model.RecordingDisclosureText),
                S["Enter the disclosure callers are told, or stop telling them."]);
        }

        if (disclosureText?.Length > ContactCenterRecordingSettings.MaxRecordingDisclosureLength)
        {
            context.Updater.ModelState.AddModelError(
                Prefix,
                nameof(model.RecordingDisclosureText),
                S["The disclosure can be at most {0} characters long.", ContactCenterRecordingSettings.MaxRecordingDisclosureLength]);
        }

        if (context.Updater.ModelState.IsValid)
        {
            settings.RecordAllCalls = model.RecordAllCalls;
            settings.ConsentModel = model.ConsentModel;
            settings.RequireExplicitConsent = model.RequireExplicitConsent;
            settings.EnableRecordingDisclosure = model.EnableRecordingDisclosure;
            settings.RecordingDisclosureText = disclosureText;
            settings.DiscloseOnInboundCalls = model.DiscloseOnInboundCalls;
            settings.DiscloseOnAIVoiceCalls = model.DiscloseOnAIVoiceCalls;
            settings.PromptAgentsToDisclose = model.PromptAgentsToDisclose;
            settings.RetentionDays = Math.Clamp(model.RetentionDays, 0, ContactCenterRecordingSettings.MaxRetentionDays);
            settings.LegalHoldByDefault = model.LegalHoldByDefault;
            settings.AllowAgentSecurePause = model.AllowAgentSecurePause;
            settings.MaxSecurePauseSeconds = Math.Clamp(model.MaxSecurePauseSeconds, 0, ContactCenterRecordingSettings.MaxSecurePauseSecondsLimit);
            settings.RequirePauseReason = model.RequirePauseReason;
        }

        return Edit(site, settings, context);
    }
}
