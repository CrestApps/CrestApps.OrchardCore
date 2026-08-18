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
            model.LiveAccountId = settings.LiveAccountId;
            model.TestPublishableKey = settings.TestPublishableKey;
            model.HasTestPrivateSecret = !string.IsNullOrEmpty(settings.TestPrivateSecret);
            model.HasTestWebhookSecret = !string.IsNullOrEmpty(settings.TestWebhookSecret);
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

        var protector = _dataProtectionProvider.CreateProtector(ProtectionPurpose);

        // IsLive and CheckoutMode are projected into the runtime StripeOptions, so a change to either must
        // reload the tenant even when no secret value changed.
        var changed =
            settings.IsLive != model.IsLive ||
            settings.CheckoutMode != model.CheckoutMode;

        settings.IsLive = model.IsLive;
        settings.CheckoutMode = model.CheckoutMode;

        changed |= UpdateEnvironment(settings, model, context, protector, isLive: true);
        changed |= UpdateEnvironment(settings, model, context, protector, isLive: false);

        if (changed)
        {
            _shellReleaseManager.RequestRelease();
        }

        return await EditAsync(site, settings, context);
    }

    private bool UpdateEnvironment(
        StripeSettings settings,
        StripeSettingsViewModel model,
        UpdateEditorContext context,
        IDataProtector protector,
        bool isLive)
    {
        var publishable = (isLive ? model.LivePublishableKey : model.TestPublishableKey)?.Trim();
        var secret = (isLive ? model.LivePrivateSecret : model.TestPrivateSecret)?.Trim();
        var webhook = (isLive ? model.LiveWebhookSecret : model.TestWebhookSecret)?.Trim();

        var publishablePrefix = isLive ? "pk_live_" : "pk_test_";
        var secretPrefix = isLive ? "sk_live_" : "sk_test_";
        var storedPublishable = isLive ? settings.LivePublishableKey : settings.TestPublishableKey;

        var changed = false;

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

        if (!string.IsNullOrEmpty(secret))
        {
            if (!secret.StartsWith(secretPrefix, StringComparison.Ordinal))
            {
                context.Updater.ModelState.AddModelError(Prefix, isLive ? nameof(model.LivePrivateSecret) : nameof(model.TestPrivateSecret), S["The secret key must start with: {0}", secretPrefix]);
            }
            else
            {
                var protectedSecret = protector.Protect(secret);

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

        if (!string.IsNullOrEmpty(webhook))
        {
            if (!webhook.StartsWith("whsec_", StringComparison.Ordinal))
            {
                context.Updater.ModelState.AddModelError(Prefix, isLive ? nameof(model.LiveWebhookSecret) : nameof(model.TestWebhookSecret), S["The webhook signing secret must start with: {0}", "whsec_"]);
            }
            else
            {
                var protectedWebhook = protector.Protect(webhook);

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
