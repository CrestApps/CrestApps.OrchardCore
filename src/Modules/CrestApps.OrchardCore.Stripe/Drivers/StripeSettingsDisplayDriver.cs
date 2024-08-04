using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Stripe.Drivers;

public sealed class StripeSettingsDisplayDriver : SectionDisplayDriver<ISite, StripeSettings>
{
    public const string ProtectionPurpose = "StripeSettings";
    public const string GroupId = "stripe";

    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    public StripeSettingsDisplayDriver(
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<StripeSettingsDisplayDriver> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _dataProtectionProvider = dataProtectionProvider;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(ISite site, StripeSettings settings, BuildEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, StripePermissions.ManageStripeSettings))
        {
            return null;
        }

        return Initialize<StripeSettingsViewModel>("StripeSettings_Edit", model =>
        {
            model.IsLive = settings.IsLive;
            model.LivePublishableKey = settings.LivePublishableKey;
            model.HasLivePrivateSecret = !string.IsNullOrEmpty(settings.LivePrivateSecret);
            model.HasLiveWebhookSecret = !string.IsNullOrEmpty(settings.LiveWebhookSecret);
            model.TestingPublishableKey = settings.TestingPublishableKey;
            model.HasTestingPrivateSecret = !string.IsNullOrEmpty(settings.TestingPrivateSecret);
            model.HasTestingWebhookSecret = !string.IsNullOrEmpty(settings.TestingWebhookSecret);
        }).Location("Content:5")
        .OnGroup(GroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, StripeSettings settings, IUpdateModel updater, UpdateEditorContext context)
    {
        if (!string.Equals(context.GroupId, GroupId, StringComparison.OrdinalIgnoreCase) ||
            !await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, StripePermissions.ManageStripeSettings))
        {
            return null;
        }

        var model = new StripeSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var protector = _dataProtectionProvider.CreateProtector(ProtectionPurpose);

        settings.LivePublishableKey = model.LivePublishableKey;

        if (model.IsLive)
        {
            if (string.IsNullOrWhiteSpace(model.LivePublishableKey))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.LivePublishableKey), S["Production publishable key is required."]);
            }

            if (!string.IsNullOrWhiteSpace(model.LivePrivateSecret))
            {
                settings.LivePrivateSecret = protector.Protect(model.LivePrivateSecret);
            }
            else if (string.IsNullOrEmpty(settings.LivePrivateSecret))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.LivePrivateSecret), S["Production private secret is required."]);
            }

            if (!string.IsNullOrWhiteSpace(model.LiveWebhookSecret))
            {
                settings.LiveWebhookSecret = protector.Protect(model.LiveWebhookSecret);
            }
            else if (string.IsNullOrEmpty(settings.LiveWebhookSecret))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.LiveWebhookSecret), S["Production Webhook private secret is required."]);
            }

            return await EditAsync(settings, context);
        }

        settings.TestingPublishableKey = model.TestingPublishableKey;

        if (string.IsNullOrWhiteSpace(model.TestingPublishableKey))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TestingPublishableKey), S["Testing publishable key is required."]);
        }

        if (!string.IsNullOrWhiteSpace(model.TestingPrivateSecret))
        {
            settings.TestingPrivateSecret = protector.Protect(model.TestingPrivateSecret);
        }
        else if (string.IsNullOrEmpty(settings.TestingPrivateSecret))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TestingPrivateSecret), S["Testing Private secret is required."]);
        }

        if (!string.IsNullOrWhiteSpace(model.TestingWebhookSecret))
        {
            settings.TestingWebhookSecret = protector.Protect(model.TestingWebhookSecret);
        }
        else if (string.IsNullOrEmpty(settings.TestingWebhookSecret))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TestingWebhookSecret), S["Testing Webhook private secret is required."]);
        }

        return await EditAsync(settings, context);
    }
}
