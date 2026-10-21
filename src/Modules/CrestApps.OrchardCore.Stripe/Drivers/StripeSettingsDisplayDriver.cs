using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Models;
using CrestApps.OrchardCore.Stripe.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Options;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Stripe.Drivers;

/// <summary>
/// Displays and updates tenant-level Stripe settings in the Orchard Core admin site settings UI.
/// </summary>
/// <remarks>
/// A value supplied by the application configuration (the <c>CrestApps_Stripe</c> section) wins over the stored one, so
/// the editor shows it as configured from the application settings and never overwrites the stored value with what the
/// form posts for it.
/// </remarks>
public sealed class StripeSettingsDisplayDriver : SiteDisplayDriver<StripeSettings>
{
    /// <summary>
    /// The data protection purpose used to protect Stripe secrets stored in site settings.
    /// </summary>
    public const string ProtectionPurpose = "StripeSettings";

    /// <summary>
    /// The site settings group identifier used for the Stripe settings editor.
    /// </summary>
    public const string GroupId = "stripe";

    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IOptionsUpdateNotifier _optionsUpdateNotifier;
    private readonly StripeConfigurationOptions _configurationOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service used to verify access to Stripe settings.</param>
    /// <param name="httpContextAccessor">The accessor used to read the current HTTP context.</param>
    /// <param name="dataProtectionProvider">The provider used to protect Stripe private keys and webhook secrets.</param>
    /// <param name="optionsUpdateNotifier">The notifier that refreshes the runtime Stripe options after settings change.</param>
    /// <param name="configurationOptions">The Stripe values supplied by the application configuration.</param>
    /// <param name="stringLocalizer">The localizer used to build validation messages.</param>
    public StripeSettingsDisplayDriver(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IDataProtectionProvider dataProtectionProvider,
        IOptionsUpdateNotifier optionsUpdateNotifier,
        IOptions<StripeConfigurationOptions> configurationOptions,
        IStringLocalizer<StripeSettingsDisplayDriver> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _dataProtectionProvider = dataProtectionProvider;
        _optionsUpdateNotifier = optionsUpdateNotifier;
        _configurationOptions = configurationOptions.Value;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the site settings group identifier handled by this display driver.
    /// </summary>
    protected override string SettingsGroupId
        => GroupId;

    /// <summary>
    /// Builds the Stripe settings editor when the current user can manage Stripe settings.
    /// </summary>
    /// <param name="site">The site whose settings are being edited.</param>
    /// <param name="settings">The current Stripe settings.</param>
    /// <param name="context">The editor build context.</param>
    /// <returns>The display result for the Stripe settings editor, or <see langword="null"/> when unauthorized.</returns>
    public override async Task<IDisplayResult> EditAsync(ISite site, StripeSettings settings, BuildEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, StripePermissions.ManageStripeSettings))
        {
            return null;
        }

        var live = _configurationOptions.GetKeys(isLive: true);
        var test = _configurationOptions.GetKeys(isLive: false);

        return Initialize<StripeSettingsViewModel>("StripeSettings_Edit", model =>
        {
            // A configured value is shown as the one in effect; a configured secret is never shown, only reported.
            model.IsLive = _configurationOptions.IsLive ?? settings.IsLive;
            model.IsLiveFromConfiguration = _configurationOptions.IsLive.HasValue;

            model.LivePublishableKey = live.HasPublishableKey ? live.PublishableKey.Trim() : settings.LivePublishableKey;
            model.LivePublishableKeyFromConfiguration = live.HasPublishableKey;
            model.HasLivePrivateSecret = live.HasSecretKey || !string.IsNullOrEmpty(settings.LivePrivateSecret);
            model.LivePrivateSecretFromConfiguration = live.HasSecretKey;
            model.HasLiveWebhookSecret = live.HasWebhookSecret || !string.IsNullOrEmpty(settings.LiveWebhookSecret);
            model.LiveWebhookSecretFromConfiguration = live.HasWebhookSecret;
            model.LiveAccountId = settings.LiveAccountId;

            model.TestPublishableKey = test.HasPublishableKey ? test.PublishableKey.Trim() : settings.TestPublishableKey;
            model.TestPublishableKeyFromConfiguration = test.HasPublishableKey;
            model.HasTestPrivateSecret = test.HasSecretKey || !string.IsNullOrEmpty(settings.TestPrivateSecret);
            model.TestPrivateSecretFromConfiguration = test.HasSecretKey;
            model.HasTestWebhookSecret = test.HasWebhookSecret || !string.IsNullOrEmpty(settings.TestWebhookSecret);
            model.TestWebhookSecretFromConfiguration = test.HasWebhookSecret;
            model.TestAccountId = settings.TestAccountId;
        }).Location("Content:5")
        .OnGroup(SettingsGroupId);
    }

    /// <summary>
    /// Updates Stripe settings from the submitted editor model and protects secret values before storage.
    /// </summary>
    /// <param name="site">The site whose settings are being updated.</param>
    /// <param name="settings">The Stripe settings to update.</param>
    /// <param name="context">The editor update context.</param>
    /// <returns>The display result for the Stripe settings editor, or <see langword="null"/> when unauthorized.</returns>
    public override async Task<IDisplayResult> UpdateAsync(ISite site, StripeSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, StripePermissions.ManageStripeSettings))
        {
            return null;
        }

        var model = new StripeSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // IsLive is projected into the runtime StripeOptions, so a change to it must refresh them even when no
        // secret value changed. The checkout mode is no longer edited here: the payment step always collects
        // the card on the site, so the stored value is left as it is. A mode set by the application configuration
        // wins, so the posted value is ignored and the stored one is left as it is.
        var changed = false;

        if (!_configurationOptions.IsLive.HasValue)
        {
            changed = settings.IsLive != model.IsLive;
            settings.IsLive = model.IsLive;
        }

        changed |= UpdateEnvironment(settings, model, context, isLive: true);
        changed |= UpdateEnvironment(settings, model, context, isLive: false);

        if (changed)
        {
            _optionsUpdateNotifier.RequestUpdate<StripeOptions>();
        }

        return await EditAsync(site, settings, context);
    }

    private bool UpdateEnvironment(
        StripeSettings settings,
        StripeSettingsViewModel model,
        UpdateEditorContext context,
        bool isLive)
    {
        var publishable = (isLive ? model.LivePublishableKey : model.TestPublishableKey)?.Trim();
        var secret = (isLive ? model.LivePrivateSecret : model.TestPrivateSecret)?.Trim();
        var webhook = (isLive ? model.LiveWebhookSecret : model.TestWebhookSecret)?.Trim();

        var publishablePrefix = isLive ? "pk_live_" : "pk_test_";
        var secretPrefix = isLive ? "sk_live_" : "sk_test_";
        var storedPublishable = isLive ? settings.LivePublishableKey : settings.TestPublishableKey;

        // A value the application configuration supplies is shown read-only and wins at runtime, so whatever the form
        // posts for it (usually nothing, since a disabled field is not posted) must never replace the stored value.
        var configured = _configurationOptions.GetKeys(isLive);

        var changed = false;

        if (!configured.HasPublishableKey)
        {
            if (!string.IsNullOrEmpty(publishable) && !publishable.StartsWith(publishablePrefix, StringComparison.Ordinal))
            {
                context.Updater.ModelState.AddModelError(Prefix, isLive ? nameof(model.LivePublishableKey) : nameof(model.TestPublishableKey), S["The publishable key must start with: {0}", publishablePrefix]);
            }
            else if (!string.Equals(storedPublishable, publishable, StringComparison.Ordinal))
            {
                if (isLive)
                {
                    settings.LivePublishableKey = publishable;
                }
                else
                {
                    settings.TestPublishableKey = publishable;
                }

                changed = true;
            }
        }

        if (!configured.HasSecretKey && !string.IsNullOrEmpty(secret))
        {
            if (!secret.StartsWith(secretPrefix, StringComparison.Ordinal))
            {
                context.Updater.ModelState.AddModelError(Prefix, isLive ? nameof(model.LivePrivateSecret) : nameof(model.TestPrivateSecret), S["The secret key must start with: {0}", secretPrefix]);
            }
            else
            {
                var protectedSecret = _dataProtectionProvider.ProtectSetting(ProtectionPurpose, secret);

                if (isLive)
                {
                    settings.LivePrivateSecret = protectedSecret;
                }
                else
                {
                    settings.TestPrivateSecret = protectedSecret;
                }

                changed = true;
            }
        }

        if (!configured.HasWebhookSecret && !string.IsNullOrEmpty(webhook))
        {
            if (!webhook.StartsWith("whsec_", StringComparison.Ordinal))
            {
                context.Updater.ModelState.AddModelError(Prefix, isLive ? nameof(model.LiveWebhookSecret) : nameof(model.TestWebhookSecret), S["The webhook signing secret must start with: {0}", "whsec_"]);
            }
            else
            {
                var protectedWebhook = _dataProtectionProvider.ProtectSetting(ProtectionPurpose, webhook);

                if (isLive)
                {
                    settings.LiveWebhookSecret = protectedWebhook;
                }
                else
                {
                    settings.TestWebhookSecret = protectedWebhook;
                }

                changed = true;
            }
        }

        return changed;
    }
}
