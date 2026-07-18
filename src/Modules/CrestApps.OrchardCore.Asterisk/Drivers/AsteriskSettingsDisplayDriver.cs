using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Asterisk.ViewModels;
using CrestApps.OrchardCore.Telephony;
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

namespace CrestApps.OrchardCore.Asterisk.Drivers;

/// <summary>
/// Display driver that renders the tenant-configured Asterisk provider settings tab on the telephony settings screen.
/// </summary>
internal sealed class AsteriskSettingsDisplayDriver : SiteDisplayDriver<AsteriskSettings>
{
    private readonly IShellReleaseManager _shellReleaseManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly INotifier _notifier;
    private readonly IAsteriskChannelTenantBindingStore _channelTenantBindingStore;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    protected override string SettingsGroupId
        => TelephonyConstants.SettingsGroupId;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="shellReleaseManager">The shell release manager.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="channelTenantBindingStore">The channel tenant binding store used to detect live calls before an ARI identity change.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AsteriskSettingsDisplayDriver(
        IShellReleaseManager shellReleaseManager,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        INotifier notifier,
        IAsteriskChannelTenantBindingStore channelTenantBindingStore,
        IHtmlLocalizer<AsteriskSettingsDisplayDriver> htmlLocalizer,
        IStringLocalizer<AsteriskSettingsDisplayDriver> stringLocalizer)
    {
        _shellReleaseManager = shellReleaseManager;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        _notifier = notifier;
        _channelTenantBindingStore = channelTenantBindingStore;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ISite site, AsteriskSettings settings, BuildEditorContext context)
    {
        return Initialize<AsteriskSettingsViewModel>("AsteriskSettings_Edit", model =>
        {
            model.IsEnabled = settings.IsEnabled;
            model.BaseUrl = settings.BaseUrl;
            model.UserName = settings.UserName;
            model.ApplicationName = settings.ApplicationName;
            model.EndpointTemplate = settings.EndpointTemplate;
            model.OutboundCallerId = settings.OutboundCallerId;
            model.TimeoutSeconds = settings.TimeoutSeconds > 0
                ? settings.TimeoutSeconds
                : AsteriskConstants.DefaultTimeoutSeconds;
            model.VoicemailContext = settings.VoicemailContext;
            model.VoicemailExtensionTemplate = settings.VoicemailExtensionTemplate;
            model.VoicemailPriority = settings.VoicemailPriority > 0
                ? settings.VoicemailPriority
                : 1;
            model.WebSocketUrl = settings.WebSocketUrl;
            model.SipDomain = settings.SipDomain;
            model.TurnUrls = settings.TurnUrls;
            model.IceTransportPolicy = string.IsNullOrWhiteSpace(settings.IceTransportPolicy)
                ? AsteriskConstants.DefaultIceTransportPolicy
                : settings.IceTransportPolicy;
            model.WebRtcCodecs = string.IsNullOrWhiteSpace(settings.WebRtcCodecs)
                ? AsteriskConstants.DefaultWebRtcCodecs
                : settings.WebRtcCodecs;
            model.PjsipCredentialLifetimeMinutes = settings.PjsipCredentialLifetimeMinutes > 0
                ? settings.PjsipCredentialLifetimeMinutes
                : AsteriskConstants.DefaultPjsipCredentialLifetimeMinutes;
            model.PjsipContactExpirationSeconds = settings.PjsipContactExpirationSeconds > 0
                ? settings.PjsipContactExpirationSeconds
                : AsteriskConstants.DefaultPjsipContactExpirationSeconds;
            model.PjsipRealtimeProviderInvariantName = settings.PjsipRealtimeProviderInvariantName;
            model.PjsipRealtimeTablePrefix = settings.PjsipRealtimeTablePrefix;
            model.HasPassword = !string.IsNullOrEmpty(settings.Password);
            model.HasTurnSharedSecret = !string.IsNullOrEmpty(settings.TurnSharedSecret);
            model.HasPjsipRealtimeConnectionString = !string.IsNullOrEmpty(settings.PjsipRealtimeConnectionString);
        }).Location("Content:10#Asterisk")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, TelephonyPermissions.ManageTelephonySettings))
        .OnGroup(SettingsGroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, AsteriskSettings settings, UpdateEditorContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (!await _authorizationService.AuthorizeAsync(user, TelephonyPermissions.ManageTelephonySettings))
        {
            return null;
        }

        var model = new AsteriskSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var hasChanges = settings.IsEnabled != model.IsEnabled;
        var telephonySettings = site.GetOrCreate<TelephonySettings>();

        // Capture the tenant's current ARI identity BEFORE it is mutated in place below. Abandoning that identity —
        // disabling the provider, or repointing it at a different ARI base URL or Stasis application — releases the
        // ownership of the old (base URL, application) pair, after which a different tenant could claim it. If live
        // channels are still bound to the old identity that would let another tenant subscribe to this tenant's
        // in-flight calls, so the abandonment is rejected while any binding still exists.
        var previousBaseUrl = settings.BaseUrl;
        var previousApplicationName = settings.ApplicationName;
        var previousIsEnabled = settings.IsEnabled;

        if (!model.IsEnabled)
        {
            if (hasChanges && telephonySettings.DefaultProviderName == AsteriskConstants.ProviderTechnicalName)
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

            var normalizedBaseUrl = AsteriskSettingsUtilities.NormalizeBaseUrl(model.BaseUrl);
            var applicationName = model.ApplicationName?.Trim();
            var timeoutSeconds = model.TimeoutSeconds > 0
                ? model.TimeoutSeconds
                : AsteriskConstants.DefaultTimeoutSeconds;
            var voicemailPriority = model.VoicemailPriority > 0
                ? model.VoicemailPriority
                : 1;
            var iceTransportPolicy = string.IsNullOrWhiteSpace(model.IceTransportPolicy)
                ? AsteriskConstants.DefaultIceTransportPolicy
                : model.IceTransportPolicy.Trim();
            var webRtcCodecs = string.IsNullOrWhiteSpace(model.WebRtcCodecs)
                ? AsteriskConstants.DefaultWebRtcCodecs
                : model.WebRtcCodecs.Trim();
            var pjsipCredentialLifetimeMinutes = model.PjsipCredentialLifetimeMinutes > 0
                ? model.PjsipCredentialLifetimeMinutes
                : AsteriskConstants.DefaultPjsipCredentialLifetimeMinutes;
            var pjsipContactExpirationSeconds = model.PjsipContactExpirationSeconds > 0
                ? model.PjsipContactExpirationSeconds
                : AsteriskConstants.DefaultPjsipContactExpirationSeconds;

            hasChanges |= settings.BaseUrl != normalizedBaseUrl;
            hasChanges |= settings.UserName != model.UserName?.Trim();
            hasChanges |= settings.ApplicationName != applicationName;
            hasChanges |= settings.EndpointTemplate != model.EndpointTemplate?.Trim();
            hasChanges |= settings.OutboundCallerId != model.OutboundCallerId?.Trim();
            hasChanges |= settings.TimeoutSeconds != timeoutSeconds;
            hasChanges |= settings.VoicemailContext != model.VoicemailContext?.Trim();
            hasChanges |= settings.VoicemailExtensionTemplate != model.VoicemailExtensionTemplate?.Trim();
            hasChanges |= settings.VoicemailPriority != voicemailPriority;
            hasChanges |= settings.WebSocketUrl != model.WebSocketUrl?.Trim();
            hasChanges |= settings.SipDomain != model.SipDomain?.Trim();
            hasChanges |= settings.TurnUrls != model.TurnUrls?.Trim();
            hasChanges |= settings.IceTransportPolicy != iceTransportPolicy;
            hasChanges |= settings.WebRtcCodecs != webRtcCodecs;
            hasChanges |= settings.PjsipCredentialLifetimeMinutes != pjsipCredentialLifetimeMinutes;
            hasChanges |= settings.PjsipContactExpirationSeconds != pjsipContactExpirationSeconds;
            hasChanges |= settings.PjsipRealtimeProviderInvariantName != model.PjsipRealtimeProviderInvariantName?.Trim();
            hasChanges |= settings.PjsipRealtimeTablePrefix != model.PjsipRealtimeTablePrefix?.Trim();

            settings.BaseUrl = normalizedBaseUrl;
            settings.UserName = model.UserName?.Trim();
            settings.ApplicationName = applicationName;
            settings.EndpointTemplate = model.EndpointTemplate?.Trim();
            settings.OutboundCallerId = model.OutboundCallerId?.Trim();
            settings.TimeoutSeconds = timeoutSeconds;
            settings.VoicemailContext = model.VoicemailContext?.Trim();
            settings.VoicemailExtensionTemplate = model.VoicemailExtensionTemplate?.Trim();
            settings.VoicemailPriority = voicemailPriority;
            settings.WebSocketUrl = model.WebSocketUrl?.Trim();
            settings.SipDomain = model.SipDomain?.Trim();
            settings.TurnUrls = model.TurnUrls?.Trim();
            settings.IceTransportPolicy = iceTransportPolicy;
            settings.WebRtcCodecs = webRtcCodecs;
            settings.PjsipCredentialLifetimeMinutes = pjsipCredentialLifetimeMinutes;
            settings.PjsipContactExpirationSeconds = pjsipContactExpirationSeconds;
            settings.PjsipRealtimeProviderInvariantName = model.PjsipRealtimeProviderInvariantName?.Trim();
            settings.PjsipRealtimeTablePrefix = model.PjsipRealtimeTablePrefix?.Trim();

            if (string.IsNullOrWhiteSpace(settings.BaseUrl) || !Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.BaseUrl), S["Enter a valid absolute Asterisk ARI URL."]);
            }

            if (string.IsNullOrWhiteSpace(settings.UserName))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.UserName), S["Enter the Asterisk ARI user name."]);
            }

            if (string.IsNullOrWhiteSpace(settings.ApplicationName))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApplicationName), S["Enter the Asterisk Stasis application name."]);
            }

            if (settings.TimeoutSeconds <= 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.TimeoutSeconds), S["Enter a timeout greater than zero."]);
            }

            if (!string.IsNullOrWhiteSpace(settings.WebSocketUrl) &&
                (!Uri.TryCreate(settings.WebSocketUrl, UriKind.Absolute, out var webSocketUri) ||
                !string.Equals(webSocketUri.Scheme, "wss", StringComparison.OrdinalIgnoreCase)))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.WebSocketUrl), S["Enter a valid secure WebSocket URL that starts with wss://."]);
            }

            if (!string.IsNullOrWhiteSpace(settings.WebSocketUrl) && string.IsNullOrWhiteSpace(settings.SipDomain))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.SipDomain), S["Enter the SIP domain for browser registrations."]);
            }

            if (AsteriskSettingsUtilities.ParseDelimitedValues(settings.WebRtcCodecs).Count == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.WebRtcCodecs), S["Enter at least one browser audio codec."]);
            }

            if (settings.PjsipCredentialLifetimeMinutes <= 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.PjsipCredentialLifetimeMinutes), S["Enter a credential lifetime greater than zero."]);
            }

            if (settings.PjsipContactExpirationSeconds <= 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.PjsipContactExpirationSeconds), S["Enter a contact expiration greater than zero."]);
            }

            if (!string.IsNullOrWhiteSpace(settings.WebSocketUrl) && string.IsNullOrWhiteSpace(settings.PjsipRealtimeProviderInvariantName))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.PjsipRealtimeProviderInvariantName), S["Enter the PJSIP Realtime ADO.NET provider invariant name."]);
            }

            if (!string.IsNullOrWhiteSpace(settings.WebSocketUrl) &&
                string.IsNullOrEmpty(settings.PjsipRealtimeConnectionString) &&
                string.IsNullOrWhiteSpace(model.PjsipRealtimeConnectionString))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.PjsipRealtimeConnectionString), S["Enter the PJSIP Realtime connection string."]);
            }

            if (!string.IsNullOrWhiteSpace(settings.PjsipRealtimeTablePrefix) &&
                !AsteriskPjsipRealtimeTablePrefixValidator.IsValid(settings.PjsipRealtimeTablePrefix))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.PjsipRealtimeTablePrefix), S["Enter a valid PJSIP Realtime table prefix using only letters, digits, or underscores, optionally qualified with a single schema name."]);
            }

            if (string.IsNullOrWhiteSpace(settings.VoicemailContext) != string.IsNullOrWhiteSpace(settings.VoicemailExtensionTemplate))
            {
                context.Updater.ModelState.AddModelError(
                    Prefix,
                    nameof(model.VoicemailExtensionTemplate),
                    S["Enter both the voicemail context and voicemail extension template, or leave both empty to disable soft-phone voicemail routing."]);
            }

            if (settings.VoicemailPriority <= 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.VoicemailPriority), S["Enter a voicemail priority greater than zero."]);
            }

            if (string.IsNullOrEmpty(settings.Password) && string.IsNullOrWhiteSpace(model.Password))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Password), S["Enter the Asterisk ARI password."]);
            }

            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                var protector = _dataProtectionProvider.CreateProtector(AsteriskConstants.ProtectorName);
                var protectedPassword = protector.Protect(model.Password);

                hasChanges |= settings.Password != protectedPassword;

                settings.Password = protectedPassword;
            }

            if (!string.IsNullOrWhiteSpace(model.TurnSharedSecret))
            {
                var protector = _dataProtectionProvider.CreateProtector(AsteriskConstants.ProtectorName);
                var protectedTurnSharedSecret = protector.Protect(model.TurnSharedSecret);

                hasChanges |= settings.TurnSharedSecret != protectedTurnSharedSecret;

                settings.TurnSharedSecret = protectedTurnSharedSecret;
            }

            if (!string.IsNullOrWhiteSpace(model.PjsipRealtimeConnectionString))
            {
                var protector = _dataProtectionProvider.CreateProtector(AsteriskConstants.ProtectorName);
                var protectedConnectionString = protector.Protect(model.PjsipRealtimeConnectionString);

                hasChanges |= settings.PjsipRealtimeConnectionString != protectedConnectionString;

                settings.PjsipRealtimeConnectionString = protectedConnectionString;
            }
        }

        // Reject abandoning the tenant's live ARI identity while calls are still bound to it. This runs after the
        // in-place mutation so it compares the freshly-applied identity against the captured prior identity using the
        // same normalization the ownership registry applies. Disabling the provider, repointing its base URL, or
        // renaming its Stasis application all abandon the old identity; while any binding still exists the change is
        // refused so another tenant cannot claim the old identity and observe this tenant's in-flight channels.
        if (previousIsEnabled &&
            IsAriIdentityAbandoned(previousBaseUrl, previousApplicationName, settings) &&
            await _channelTenantBindingStore.HasAnyAsync())
        {
            context.Updater.ModelState.AddModelError(
                Prefix,
                S["This Asterisk endpoint still owns one or more live call channels. End or reconcile those calls before you disable the provider or change its ARI URL or Stasis application name."]);
        }

        if (context.Updater.ModelState.IsValid && settings.IsEnabled && string.IsNullOrEmpty(telephonySettings.DefaultProviderName))
        {
            telephonySettings.DefaultProviderName = AsteriskConstants.ProviderTechnicalName;

            site.Put(telephonySettings);

            hasChanges = true;
        }

        if (hasChanges)
        {
            _shellReleaseManager.RequestRelease();
        }

        return Edit(site, settings, context);
    }

    private static bool IsAriIdentityAbandoned(string previousBaseUrl, string previousApplicationName, AsteriskSettings settings)
    {
        if (!settings.IsEnabled)
        {
            return true;
        }

        var previousNormalizedBaseUrl = AsteriskSettingsUtilities.NormalizeBaseUrl(previousBaseUrl);
        var currentNormalizedBaseUrl = AsteriskSettingsUtilities.NormalizeBaseUrl(settings.BaseUrl);

        if (!string.Equals(previousNormalizedBaseUrl, currentNormalizedBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.Equals(previousApplicationName?.Trim(), settings.ApplicationName?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
