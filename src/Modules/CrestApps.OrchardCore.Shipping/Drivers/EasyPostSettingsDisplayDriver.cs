using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Shipping.ViewModels;
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
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Shipping.Drivers;

/// <summary>
/// The EasyPost tab of Settings › Commerce › Shipping: enabling the provider and its API key. Tracking updates are
/// set up here too, without the store entering anything: when the site has a public base URL, saving registers a
/// webhook with EasyPost, signed with a secret made for it.
/// </summary>
public sealed class EasyPostSettingsDisplayDriver : SiteDisplayDriver<EasyPostSettings>
{
    private readonly EasyPostCarrierProvider _provider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly INotifier _notifier;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    public EasyPostSettingsDisplayDriver(
        EasyPostCarrierProvider provider,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IDataProtectionProvider dataProtectionProvider,
        INotifier notifier,
        IStringLocalizer<EasyPostSettingsDisplayDriver> stringLocalizer,
        IHtmlLocalizer<EasyPostSettingsDisplayDriver> htmlLocalizer)
    {
        _provider = provider;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _dataProtectionProvider = dataProtectionProvider;
        _notifier = notifier;
        S = stringLocalizer;
        H = htmlLocalizer;
    }

    protected override string SettingsGroupId
        => ShippingConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, EasyPostSettings settings, BuildEditorContext context)
        => Initialize<EasyPostSettingsViewModel>("EasyPostSettings_Edit", model =>
        {
            model.IsEnabled = settings.IsEnabled;
            model.HasApiKey = !string.IsNullOrEmpty(settings.ApiKey);
            model.WebhookUrl = settings.WebhookId is null ? null : settings.WebhookUrl;
            model.PublicWebhookUrl = WebhookUrlFor(site.BaseUrl);
        }).Location("Content:5#EasyPost")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ShippingPermissions.ManageShipping))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, EasyPostSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ShippingPermissions.ManageShipping))
        {
            return null;
        }

        var model = new EasyPostSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var providers = site.GetOrCreate<CarrierProviderSettings>();
        var protector = _dataProtectionProvider.CreateProtector(EasyPostCarrierProvider.ProtectionPurpose);

        if (!model.IsEnabled)
        {
            if (settings.IsEnabled && string.Equals(providers.DefaultProviderName, EasyPostCarrierProvider.TechnicalName, StringComparison.OrdinalIgnoreCase))
            {
                await _notifier.WarningAsync(H["EasyPost was the default carrier provider. Carrier rates and labels are off until you choose another default provider."]);

                providers.DefaultProviderName = null;
                site.Put(providers);
            }

            settings.IsEnabled = false;

            return Edit(site, settings, context);
        }

        settings.IsEnabled = true;

        var newKey = string.IsNullOrWhiteSpace(model.ApiKey) ? null : model.ApiKey.Trim();

        if (newKey is null && string.IsNullOrEmpty(settings.ApiKey))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ApiKey), S["Enter the API key from your EasyPost dashboard."]);

            return Edit(site, settings, context);
        }

        if (newKey is not null)
        {
            settings.ApiKey = protector.Protect(newKey);
        }

        await RegisterWebhookAsync(site, settings, newKey ?? _provider.Unprotect(settings.ApiKey), keyChanged: newKey is not null);

        // Enabling the only provider makes it the default.
        if (string.IsNullOrEmpty(providers.DefaultProviderName))
        {
            providers.DefaultProviderName = EasyPostCarrierProvider.TechnicalName;
            site.Put(providers);
        }

        return Edit(site, settings, context);
    }

    // A webhook for the site's public address, made once and again when the address or the account changes. Saving
    // doesn't fail when EasyPost refuses it: the store is told why, and rates and labels work without it.
    private async Task RegisterWebhookAsync(ISite site, EasyPostSettings settings, string apiKey, bool keyChanged)
    {
        var url = WebhookUrlFor(site.BaseUrl);

        if (url is null || string.IsNullOrEmpty(apiKey) || (!keyChanged && settings.WebhookId is not null && settings.WebhookUrl == url))
        {
            return;
        }

        // The old webhook belongs to the same account unless the key changed; then it's the other account's to remove.
        if (!keyChanged && settings.WebhookId is not null)
        {
            await _provider.DeleteWebhookAsync(apiKey, settings.WebhookId);
        }

        var (webhookId, secret, error) = await _provider.RegisterWebhookAsync(apiKey, url);

        if (webhookId is null)
        {
            await _notifier.WarningAsync(H["EasyPost didn't register the tracking updates webhook: {0}", error]);

            return;
        }

        settings.WebhookId = webhookId;
        settings.WebhookUrl = url;
        settings.WebhookSecret = _dataProtectionProvider.CreateProtector(EasyPostCarrierProvider.ProtectionPurpose).Protect(secret);
    }

    // EasyPost can only call a public https address, never the developer's own machine.
    internal static string WebhookUrlFor(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.IsLoopback ||
            uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".test", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return baseUrl.TrimEnd('/') + "/" + ShippingConstants.EasyPostWebhookPath;
    }
}
