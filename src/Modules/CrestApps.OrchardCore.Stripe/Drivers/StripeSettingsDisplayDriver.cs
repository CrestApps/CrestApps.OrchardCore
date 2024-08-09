using CrestApps.OrchardCore.Stripe.Core;
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

public sealed class StripeSettingsDisplayDriver : SiteDisplayDriver<StripeSettings>
{
    public const string ProtectionPurpose = "StripeSettings";
    public const string GroupId = "stripe";

    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IShellReleaseManager _shellReleaseManager;

    internal readonly IStringLocalizer S;

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

    protected override string SettingsGroupId
        => GroupId;

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
            model.LivePublishableKey = settings.LivePublishableKey;
            model.HasLivePrivateSecret = !string.IsNullOrEmpty(settings.LivePrivateSecret);
            model.HasLiveWebhookSecret = !string.IsNullOrEmpty(settings.LiveWebhookSecret);
            model.TestPublishableKey = settings.TestPublishableKey;
            model.HasTestPrivateSecret = !string.IsNullOrEmpty(settings.TestPrivateSecret);
            model.HasTestWebhookSecret = !string.IsNullOrEmpty(settings.TestWebhookSecret);
        }).Location("Content:5")
        .OnGroup(SettingsGroupId);
    }

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
                if (!model.LivePublishableKey.StartsWith("sk_live_", StringComparison.Ordinal))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.LivePublishableKey), S["Production secret key must start with: {0}", "sk_live_"]);
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
