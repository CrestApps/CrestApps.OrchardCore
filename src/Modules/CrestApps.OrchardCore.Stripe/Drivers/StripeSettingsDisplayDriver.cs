using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Models;
using CrestApps.OrchardCore.Stripe.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Stripe.Drivers;

/// <summary>
/// Displays and updates tenant-level Stripe settings in the Orchard Core admin site settings UI.
/// </summary>
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
    private readonly IShellReleaseManager _shellReleaseManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeSettingsDisplayDriver"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service used to verify access to Stripe settings.</param>
    /// <param name="httpContextAccessor">The accessor used to read the current HTTP context.</param>
    /// <param name="dataProtectionProvider">The provider used to protect Stripe private keys and webhook secrets.</param>
    /// <param name="shellReleaseManager">The manager used to request a tenant reload after settings change.</param>
    /// <param name="stringLocalizer">The localizer used to build validation messages.</param>
    public StripeSettingsDisplayDriver(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IDataProtectionProvider dataProtectionProvider,
        IShellReleaseManager shellReleaseManager,
        IStringLocalizer<StripeSettingsDisplayDriver> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _dataProtectionProvider = dataProtectionProvider;
        _shellReleaseManager = shellReleaseManager;
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

        context.AddTenantReloadWarningWrapper();

        return Initialize<StripeSettingsViewModel>("StripeSettings_Edit", model =>
        {
            model.IsLive = settings.IsLive;
            model.CheckoutMode = settings.CheckoutMode;
            model.LivePublishableKey = settings.LivePublishableKey;
            model.HasLivePrivateSecret = !string.IsNullOrEmpty(settings.LivePrivateSecret);
            model.HasLiveWebhookSecret = !string.IsNullOrEmpty(settings.LiveWebhookSecret);
            model.TestPublishableKey = settings.TestPublishableKey;
            model.HasTestPrivateSecret = !string.IsNullOrEmpty(settings.TestPrivateSecret);
            model.HasTestWebhookSecret = !string.IsNullOrEmpty(settings.TestWebhookSecret);
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

        var protector = _dataProtectionProvider.CreateProtector(ProtectionPurpose);

        _shellReleaseManager.RequestRelease();

        settings.IsLive = model.IsLive;
        settings.CheckoutMode = model.CheckoutMode;

        if (model.IsLive)
        {
            var liveUpdated = settings.LivePublishableKey != model.LivePublishableKey;

            if (string.IsNullOrWhiteSpace(model.LivePublishableKey))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.LivePublishableKey), S["Production publishable key is required."]);
            }
            else if (!model.LivePublishableKey.StartsWith("pk_live_", StringComparison.Ordinal))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.LivePublishableKey), S["Production publishable key must start with: {0}", "pk_live_"]);
            }

            if (!string.IsNullOrWhiteSpace(model.LivePrivateSecret))
            {
                if (!model.LivePrivateSecret.StartsWith("sk_live_", StringComparison.Ordinal))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.LivePrivateSecret), S["Production secret key must start with: {0}", "sk_live_"]);
                }
                else
                {
                    settings.LivePrivateSecret = protector.Protect(model.LivePrivateSecret);
                    liveUpdated = true;
                }
            }
            else if (string.IsNullOrEmpty(settings.LivePrivateSecret))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.LivePrivateSecret), S["Production secret key is required."]);
            }

            if (!string.IsNullOrWhiteSpace(model.LiveWebhookSecret))
            {
                if (!model.LiveWebhookSecret.StartsWith("whsec_", StringComparison.Ordinal))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.LiveWebhookSecret), S["Production Webhooks secret must start with: {0}", "whsec_"]);
                }
                else
                {
                    settings.LiveWebhookSecret = protector.Protect(model.LiveWebhookSecret);
                    liveUpdated = true;
                }
            }
            else if (string.IsNullOrEmpty(settings.LiveWebhookSecret))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.LiveWebhookSecret), S["Production Webhooks secret is required."]);
            }

            settings.LivePublishableKey = model.LivePublishableKey;

            if (liveUpdated)
            {
                _shellReleaseManager.RequestRelease();
            }

            return await EditAsync(site, settings, context);
        }

        var testingUpdated = settings.TestPublishableKey != model.TestPublishableKey;

        if (string.IsNullOrWhiteSpace(model.TestPublishableKey))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TestPublishableKey), S["Test publishable key is required."]);
        }
        else if (!model.TestPublishableKey.StartsWith("pk_test_", StringComparison.Ordinal))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TestPublishableKey), S["Test publishable key must start with: {0}", "pk_test_"]);
        }

        if (!string.IsNullOrWhiteSpace(model.TestPrivateSecret))
        {
            if (!model.TestPrivateSecret.StartsWith("sk_test_", StringComparison.Ordinal))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.TestPrivateSecret), S["Test secret key must start with: {0}", "sk_test_"]);
            }
            else
            {
                settings.TestPrivateSecret = protector.Protect(model.TestPrivateSecret);
                testingUpdated = true;
            }
        }
        else if (string.IsNullOrEmpty(settings.TestPrivateSecret))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TestPrivateSecret), S["Test Private secret key is required."]);
        }

        if (!string.IsNullOrWhiteSpace(model.TestWebhookSecret))
        {
            if (!model.TestWebhookSecret.StartsWith("whsec_", StringComparison.Ordinal))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.TestWebhookSecret), S["Test Webhooks secret must start with: {0}", "whsec_"]);
            }
            else
            {
                settings.TestWebhookSecret = protector.Protect(model.TestWebhookSecret);
                testingUpdated = true;
            }
        }
        else if (string.IsNullOrEmpty(settings.TestWebhookSecret))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TestWebhookSecret), S["Test Webhooks secret is required."]);
        }

        settings.TestPublishableKey = model.TestPublishableKey;

        if (testingUpdated)
        {
            _shellReleaseManager.RequestRelease();
        }

        return await EditAsync(site, settings, context);
    }
}
