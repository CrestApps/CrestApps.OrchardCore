using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telnyx.Drivers;

/// <summary>
/// Display driver that renders the Telnyx provider settings tab on the telephony settings screen.
/// </summary>
public sealed class TelnyxSettingsDisplayDriver : SiteDisplayDriver<TelnyxSettings>
{
    private readonly IShellReleaseManager _shellReleaseManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    protected override string SettingsGroupId
        => TelephonyConstants.SettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxSettingsDisplayDriver"/> class.
    /// </summary>
    public TelnyxSettingsDisplayDriver(
        IShellReleaseManager shellReleaseManager,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        INotifier notifier,
        IHtmlLocalizer<TelnyxSettingsDisplayDriver> htmlLocalizer,
        IStringLocalizer<TelnyxSettingsDisplayDriver> stringLocalizer)
    {
        _shellReleaseManager = shellReleaseManager;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ISite site, TelnyxSettings settings, BuildEditorContext context)
    {
        Task<bool> CanManageAsync()
            => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TelephonyPermissions.ManageTelephonySettings);

        return Initialize<TelnyxSettingsViewModel>("TelnyxSettings_Edit", model =>
        {
            model.IsEnabled = settings.IsEnabled;
            model.IsConnected = !string.IsNullOrWhiteSpace(settings.ConnectionId) && !string.IsNullOrWhiteSpace(settings.SipConnectionId);
            model.ConnectionId = settings.ConnectionId;
            model.SipConnectionId = settings.SipConnectionId;
            model.OutboundVoiceProfileId = settings.OutboundVoiceProfileId;
            model.DefaultOutboundCallerId = settings.DefaultOutboundCallerId;
            model.CredentialLifetimeMinutes = settings.CredentialLifetimeMinutes > 0 ? settings.CredentialLifetimeMinutes : 60;
            model.SipWebSocketUrl = settings.SipWebSocketUrl;
            model.SipDomain = settings.SipDomain;
            model.WebRtcCodecs = settings.WebRtcCodecs;
            model.IceUrls = settings.IceUrls;
            model.TurnUsername = settings.TurnUsername;
            model.IceTransportPolicy = settings.IceTransportPolicy;
            model.ApiBaseUrl = settings.ApiBaseUrl;
            model.HasApiKey = !string.IsNullOrEmpty(settings.ApiKey);
            model.HasWebhookPublicKey = !string.IsNullOrEmpty(settings.WebhookPublicKey);
            model.HasTurnCredential = !string.IsNullOrEmpty(settings.TurnCredential);
            model.WebhookPath = "/" + TelnyxConstants.WebhookPath;
        }).Location("Content:10#Telnyx;1")
        .RenderWhen(CanManageAsync)
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, TelnyxSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, TelephonyPermissions.ManageTelephonySettings))
        {
            return null;
        }

        var model = new TelnyxSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var hasChanges = settings.IsEnabled != model.IsEnabled;
        var telephonySettings = site.GetOrCreate<TelephonySettings>();

        if (!model.IsEnabled)
        {
            if (hasChanges && telephonySettings.DefaultProviderName == TelnyxConstants.ProviderTechnicalName)
            {
                await _notifier.WarningAsync(H["You have disabled the default telephony provider. The soft phone is now disabled until you designate a new default provider."]);

                telephonySettings.DefaultProviderName = null;

                site.Put(telephonySettings);
            }

            settings.IsEnabled = false;
        }
        else
        {
            settings.IsEnabled = true;

            // The connection ids (Call Control, SIP, outbound voice profile) are managed by the Connect
            // flow, not this form, so they are never read back from the model here — that keeps a plain Save
            // from wiping the provisioned ids.
            hasChanges |= settings.DefaultOutboundCallerId != Trim(model.DefaultOutboundCallerId);
            hasChanges |= settings.SipWebSocketUrl != Trim(model.SipWebSocketUrl);
            hasChanges |= settings.SipDomain != Trim(model.SipDomain);
            hasChanges |= settings.WebRtcCodecs != Trim(model.WebRtcCodecs);
            hasChanges |= settings.IceUrls != Trim(model.IceUrls);
            hasChanges |= settings.TurnUsername != Trim(model.TurnUsername);
            hasChanges |= settings.IceTransportPolicy != Trim(model.IceTransportPolicy);
            hasChanges |= settings.ApiBaseUrl != Trim(model.ApiBaseUrl);
            hasChanges |= settings.CredentialLifetimeMinutes != NormalizeLifetime(model.CredentialLifetimeMinutes);

            settings.DefaultOutboundCallerId = Trim(model.DefaultOutboundCallerId);
            settings.SipWebSocketUrl = Trim(model.SipWebSocketUrl);
            settings.SipDomain = Trim(model.SipDomain);
            settings.WebRtcCodecs = Trim(model.WebRtcCodecs);
            settings.IceUrls = Trim(model.IceUrls);
            settings.TurnUsername = Trim(model.TurnUsername);
            settings.IceTransportPolicy = Trim(model.IceTransportPolicy);
            settings.ApiBaseUrl = Trim(model.ApiBaseUrl);
            settings.CredentialLifetimeMinutes = NormalizeLifetime(model.CredentialLifetimeMinutes);

            hasChanges |= ProtectInto(model.ApiKey, TelnyxConstants.ProtectorName, value => settings.ApiKey = value, settings.ApiKey);
            hasChanges |= ProtectInto(model.WebhookPublicKey, TelnyxConstants.WebhookProtectorName, value => settings.WebhookPublicKey = value, settings.WebhookPublicKey);
            hasChanges |= ProtectInto(model.TurnCredential, TelnyxConstants.ProtectorName, value => settings.TurnCredential = value, settings.TurnCredential);

            ValidateActiveSettings(settings, model, context);
        }

        if (context.Updater.ModelState.IsValid && settings.IsEnabled && string.IsNullOrEmpty(telephonySettings.DefaultProviderName))
        {
            telephonySettings.DefaultProviderName = TelnyxConstants.ProviderTechnicalName;

            site.Put(telephonySettings);

            hasChanges = true;
        }

        if (hasChanges)
        {
            _shellReleaseManager.RequestRelease();
        }

        return Edit(site, settings, context);
    }

    private void ValidateActiveSettings(TelnyxSettings settings, TelnyxSettingsViewModel model, UpdateEditorContext context)
    {
        // Saving requires only the API key. The connection ids are provisioned by the Connect action, and
        // the webhook public key is pasted after connecting, so neither blocks the initial save.
        if (string.IsNullOrEmpty(settings.ApiKey))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApiKey), S["Enter the Telnyx API key, then save and click Connect Telnyx."]);
        }
    }

    private bool ProtectInto(string newValue, string protectorName, Action<string> setter, string currentValue)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            return false;
        }

        var protectedValue = _dataProtectionProvider.CreateProtector(protectorName).Protect(newValue.Trim());
        setter(protectedValue);

        return protectedValue != currentValue;
    }

    private static string Trim(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int NormalizeLifetime(int minutes)
        => minutes > 0 ? minutes : 60;
}
