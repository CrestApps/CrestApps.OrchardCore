using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Shipping.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Shipping.Drivers;

/// <summary>
/// The Providers tab of Settings › Commerce › Shipping: which of the enabled carrier providers the store uses. Each
/// provider has its own tab, where it's enabled and set up.
/// </summary>
public sealed class CarrierProviderSettingsDisplayDriver : SiteDisplayDriver<CarrierProviderSettings>
{
    private readonly IEnumerable<ICarrierProvider> _providers;
    private readonly ICarrierProviderResolver _resolver;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public CarrierProviderSettingsDisplayDriver(
        IEnumerable<ICarrierProvider> providers,
        ICarrierProviderResolver resolver,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<CarrierProviderSettingsDisplayDriver> stringLocalizer)
    {
        _providers = providers;
        _resolver = resolver;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => ShippingConstants.SettingsGroupId;

    public override IDisplayResult Edit(ISite site, CarrierProviderSettings settings, BuildEditorContext context)
        => Initialize<CarrierProviderSettingsViewModel>("CarrierProviderSettings_Edit", async model =>
        {
            model.DefaultProvider = settings.DefaultProviderName;
            model.Providers = (await _resolver.GetEnabledAsync())
                .Select(provider => new SelectListItem(provider.DisplayName, provider.Name))
                .OrderBy(item => item.Text)
                .ToArray();
            model.HasProviders = _providers.Any();
        }).Location("Content:1#Providers")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ShippingPermissions.ManageShipping))
        .OnGroup(SettingsGroupId);

    public override async Task<IDisplayResult> UpdateAsync(ISite site, CarrierProviderSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ShippingPermissions.ManageShipping))
        {
            return null;
        }

        var model = new CarrierProviderSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(model.DefaultProvider))
        {
            settings.DefaultProviderName = null;
        }
        else if (_providers.Any(provider => string.Equals(provider.Name, model.DefaultProvider, StringComparison.OrdinalIgnoreCase)))
        {
            settings.DefaultProviderName = model.DefaultProvider;
        }
        else
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DefaultProvider), S["Choose one of the enabled carrier providers."]);
        }

        return Edit(site, settings, context);
    }
}
