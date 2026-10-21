using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Core.Validation;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Shipping.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Settings;
using QueryContext = CrestApps.Core.Models.QueryContext;

namespace CrestApps.OrchardCore.Shipping.Controllers;

/// <summary>
/// Commerce > Shipping: the zones, the methods each zone offers, and a page to try what an address would pay.
/// </summary>
[Admin]
public sealed class ShippingZonesController : Controller
{
    private const string OptionsSearch = "Options.Search";
    private const string NameFieldName = "Name";

    private readonly INamedCatalogManager<ShippingZone> _manager;
    private readonly INamedCatalog<ShippingZone> _catalog;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<ShippingZone> _zoneDisplayManager;
    private readonly IDisplayManager<ShippingMethod> _methodDisplayManager;
    private readonly IEnumerable<IShippingRateProvider> _providers;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public ShippingZonesController(
        INamedCatalogManager<ShippingZone> manager,
        INamedCatalog<ShippingZone> catalog,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<ShippingZone> zoneDisplayManager,
        IDisplayManager<ShippingMethod> methodDisplayManager,
        IEnumerable<IShippingRateProvider> providers,
        INotifier notifier,
        IHtmlLocalizer<ShippingZonesController> htmlLocalizer,
        IStringLocalizer<ShippingZonesController> stringLocalizer)
    {
        _manager = manager;
        _catalog = catalog;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _zoneDisplayManager = zoneDisplayManager;
        _methodDisplayManager = methodDisplayManager;
        _providers = providers;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("shipping/zones", "ShippingZonesIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ShippingPermissions.ManageShipping))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);

        // Zones are listed in the order they are tried.
        var all = (await _catalog.GetAllAsync())
            .Where(zone => string.IsNullOrEmpty(options.Search) || zone.Name?.Contains(options.Search, StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(zone => zone.RestOfWorld)
            .ThenBy(zone => zone.Position)
            .ThenBy(zone => zone.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var page = all.Skip((pager.Page - 1) * pager.PageSize).Take(pager.PageSize).ToArray();
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(OptionsSearch, options.Search);
        }

        var viewModel = new ListCatalogEntryViewModel<CatalogEntryViewModel<ShippingZone>>
        {
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, all.Length, routeData),
        };

        viewModel.List = new AdminListContext<ShippingZone>
        {
            Source = page,
            Pager = (IShape)viewModel.Pager,
        };

        viewModel.Options.BulkActions = [];

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("shipping/zones", "ShippingZonesIndex")]
    public IActionResult IndexFilterPost(ListCatalogEntryViewModel model)
        => RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { OptionsSearch, model.Options?.Search },
        });

    [Admin("shipping/zones/create", "ShippingZonesCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ShippingPermissions.ManageShipping))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = S["Shipping Zone"],
            Editor = await _zoneDisplayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        });
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("shipping/zones/create", "ShippingZonesCreate")]
    public async Task<IActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ShippingPermissions.ManageShipping))
        {
            return Forbid();
        }

        var model = await _manager.NewAsync();

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = S["Shipping Zone"],
            Editor = await _zoneDisplayManager.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (await CatalogEntryValidation.ValidateAsync(_manager, model, _updateModelAccessor.ModelUpdater, nameof(ShippingZone)) && ModelState.IsValid)
        {
            if (await _catalog.FindByNameAsync(model.Name) is not null)
            {
                ModelState.AddModelError(NameFieldName, S["A shipping zone with the same name already exists."]);
            }

            if (ModelState.IsValid)
            {
                await _manager.CreateAsync(model);
                await _notifier.SuccessAsync(H["The shipping zone has been created. Add the methods buyers in it can choose from."]);

                // On to the zone's page, where its methods are added.
                return RedirectToAction(nameof(Edit), new { id = model.ItemId });
            }
        }

        return View(viewModel);
    }

    [Admin("shipping/zones/edit/{id}", "ShippingZonesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ShippingPermissions.ManageShipping))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model is null)
        {
            return NotFound();
        }

        return View(new EditCatalogEntryViewModel
        {
            DisplayName = model.Name,
            Editor = await _zoneDisplayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        });
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("shipping/zones/edit/{id}", "ShippingZonesEdit")]
    public async Task<IActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ShippingPermissions.ManageShipping))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model is null)
        {
            return NotFound();
        }

        var viewModel = new EditCatalogEntryViewModel
        {
            DisplayName = model.Name,
            Editor = await _zoneDisplayManager.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (await CatalogEntryValidation.ValidateAsync(_manager, model, _updateModelAccessor.ModelUpdater, nameof(ShippingZone)) && ModelState.IsValid)
        {
            var existing = await _catalog.FindByNameAsync(model.Name);

            if (existing is not null && !string.Equals(existing.ItemId, model.ItemId, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(NameFieldName, S["A shipping zone with the same name already exists."]);
            }

            if (ModelState.IsValid)
            {
                await _manager.UpdateAsync(model);
                await _notifier.SuccessAsync(H["The shipping zone has been updated."]);

                // Back to the zone, so regions of newly added countries can be chosen.
                return RedirectToAction(nameof(Edit), new { id });
            }
        }

        return View(viewModel);
    }

    [HttpPost]
    [ActionName("Delete")]
    [Admin("shipping/zones/delete/{id}", "ShippingZonesDelete")]
    public async Task<IActionResult> DeletePost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ShippingPermissions.ManageShipping))
        {
            return Forbid();
        }

        var model = await _manager.FindByIdAsync(id);

        if (model is null)
        {
            return NotFound();
        }

        if (await _manager.DeleteAsync(model))
        {
            await _notifier.SuccessAsync(H["The shipping zone has been deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    [Admin("shipping/zones/{zoneId}/methods/create/{providerName}", "ShippingMethodsCreate")]
    public async Task<IActionResult> CreateMethod(string zoneId, string providerName)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ShippingPermissions.ManageShipping))
        {
            return Forbid();
        }

        var zone = await _manager.FindByIdAsync(zoneId);
        var provider = FindProvider(providerName);

        if (zone is null || provider is null)
        {
            return NotFound();
        }

        var method = NewMethod(zone, provider);

        return View("EditMethod", await BuildMethodViewModelAsync(zone, method, provider, isNew: true, update: false));
    }

    [HttpPost]
    [ActionName(nameof(CreateMethod))]
    [Admin("shipping/zones/{zoneId}/methods/create/{providerName}", "ShippingMethodsCreate")]
    public async Task<IActionResult> CreateMethodPost(string zoneId, string providerName)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ShippingPermissions.ManageShipping))
        {
            return Forbid();
        }

        var zone = await _manager.FindByIdAsync(zoneId);
        var provider = FindProvider(providerName);

        if (zone is null || provider is null)
        {
            return NotFound();
        }

        var method = NewMethod(zone, provider);
        var viewModel = await BuildMethodViewModelAsync(zone, method, provider, isNew: true, update: true);

        if (!ModelState.IsValid)
        {
            return View("EditMethod", viewModel);
        }

        zone.Methods.Add(method);
        await _manager.UpdateAsync(zone);
        await _notifier.SuccessAsync(H["The shipping method {0} has been added.", method.Name]);

        return RedirectToAction(nameof(Edit), new { id = zoneId });
    }

    [Admin("shipping/zones/{zoneId}/methods/{methodId}", "ShippingMethodsEdit")]
    public async Task<IActionResult> EditMethod(string zoneId, string methodId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ShippingPermissions.ManageShipping))
        {
            return Forbid();
        }

        var zone = await _manager.FindByIdAsync(zoneId);
        var method = zone?.FindMethod(methodId);

        if (method is null)
        {
            return NotFound();
        }

        return View(await BuildMethodViewModelAsync(zone, method, FindProvider(method.ProviderName), isNew: false, update: false));
    }

    [HttpPost]
    [ActionName(nameof(EditMethod))]
    [Admin("shipping/zones/{zoneId}/methods/{methodId}", "ShippingMethodsEdit")]
    public async Task<IActionResult> EditMethodPost(string zoneId, string methodId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ShippingPermissions.ManageShipping))
        {
            return Forbid();
        }

        var zone = await _manager.FindByIdAsync(zoneId);
        var method = zone?.FindMethod(methodId);

        if (method is null)
        {
            return NotFound();
        }

        var viewModel = await BuildMethodViewModelAsync(zone, method, FindProvider(method.ProviderName), isNew: false, update: true);

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        await _manager.UpdateAsync(zone);
        await _notifier.SuccessAsync(H["The shipping method {0} has been updated.", method.Name]);

        return RedirectToAction(nameof(Edit), new { id = zoneId });
    }

    [HttpPost]
    [Admin("shipping/zones/{zoneId}/methods/{methodId}/delete", "ShippingMethodsDelete")]
    public async Task<IActionResult> DeleteMethod(string zoneId, string methodId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ShippingPermissions.ManageShipping))
        {
            return Forbid();
        }

        var zone = await _manager.FindByIdAsync(zoneId);
        var method = zone?.FindMethod(methodId);

        if (method is null)
        {
            return NotFound();
        }

        zone.Methods.Remove(method);
        await _manager.UpdateAsync(zone);
        await _notifier.SuccessAsync(H["The shipping method {0} has been deleted.", method.Name]);

        return RedirectToAction(nameof(Edit), new { id = zoneId });
    }

    /// <summary>
    /// Shows which zone an address falls in and what each of its methods would charge, without placing an order.
    /// </summary>
    [Admin("shipping/rates", "ShippingRates")]
    public async Task<IActionResult> Rates(
        ShippingRatesTestViewModel model,
        [FromServices] IShippingRateService rateService,
        [FromServices] ICountryService countryService,
        [FromServices] IProductCurrencyProvider currencyProvider,
        [FromServices] ISiteService siteService)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ShippingPermissions.ManageShipping))
        {
            return Forbid();
        }

        model ??= new ShippingRatesTestViewModel();

        var settings = await siteService.GetSettingsAsync<ShippingSettings>();
        var currencies = await currencyProvider.GetCurrenciesAsync();

        model.Currency ??= currencies.Count > 0 ? currencies[0].CurrencyCode : null;
        model.Countries = (await countryService.GetCountriesAsync())
            .Select(country => new SelectListItem(country.Name, country.Code, string.Equals(country.Code, model.Country, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        model.Currencies = currencies
            .Select(currency => new SelectListItem(currency.CurrencyCode, currency.CurrencyCode, string.Equals(currency.CurrencyCode, model.Currency, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        model.ShippingClasses = settings.ShippingClasses ?? [];
        model.WeightUnit = settings.WeightUnit;

        if (!string.IsNullOrEmpty(model.Country))
        {
            var request = new ShippingRateRequest
            {
                Destination = new Address { Country = model.Country, Region = string.IsNullOrWhiteSpace(model.Region) ? null : model.Region.Trim().ToUpperInvariant() },
                Currency = model.Currency,
                Lines =
                [
                    new ShippingRequestLine
                    {
                        Title = S["Test item"],
                        Quantity = Math.Clamp(model.Items, 1, 10_000),
                        UnitPrice = Math.Max(0, model.Subtotal) / Math.Clamp(model.Items, 1, 10_000),
                        Weight = Math.Max(0, model.Weight) / Math.Clamp(model.Items, 1, 10_000),
                        ShippingClass = string.IsNullOrWhiteSpace(model.ShippingClass) ? null : model.ShippingClass,
                    },
                ],
            };

            model.Submitted = true;
            model.Zone = await rateService.FindZoneAsync(request.Destination);
            model.Rates = await rateService.GetRatesAsync(request);
        }

        return View(model);
    }

    private IShippingRateProvider FindProvider(string providerName)
        => _providers.FirstOrDefault(provider => string.Equals(provider.Name, providerName, StringComparison.OrdinalIgnoreCase));

    private static ShippingMethod NewMethod(ShippingZone zone, IShippingRateProvider provider)
        => new()
        {
            MethodId = UniqueId.GenerateId(),
            ProviderName = provider.Name,
            Name = provider.DisplayName,
            Position = zone.Methods.Count == 0 ? 0 : zone.Methods.Max(method => method.Position) + 1,
        };

    private async Task<EditShippingMethodViewModel> BuildMethodViewModelAsync(ShippingZone zone, ShippingMethod method, IShippingRateProvider provider, bool isNew, bool update)
        => new()
        {
            ZoneId = zone.ItemId,
            ZoneName = zone.Name,
            MethodId = method.MethodId,
            ProviderName = method.ProviderName,
            ProviderDisplayName = provider?.DisplayName ?? method.ProviderName,
            Editor = update
                ? await _methodDisplayManager.UpdateEditorAsync(method, _updateModelAccessor.ModelUpdater, isNew)
                : await _methodDisplayManager.BuildEditorAsync(method, _updateModelAccessor.ModelUpdater, isNew),
        };
}
