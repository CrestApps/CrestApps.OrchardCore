using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Shipping.Handlers;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Shipping.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Shipping.Drivers;

internal sealed class ShippingZoneDisplayDriver : DisplayDriver<ShippingZone>
{
    private readonly ICountryService _countryService;
    private readonly IAddressFormatProvider _formatProvider;
    private readonly IEnumerable<IShippingRateProvider> _providers;

    internal readonly IStringLocalizer S;

    public ShippingZoneDisplayDriver(
        ICountryService countryService,
        IAddressFormatProvider formatProvider,
        IEnumerable<IShippingRateProvider> providers,
        IStringLocalizer<ShippingZoneDisplayDriver> stringLocalizer)
    {
        _countryService = countryService;
        _formatProvider = formatProvider;
        _providers = providers;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(ShippingZone zone, BuildDisplayContext context)
    {
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = zone.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("ShippingZone_Fields_SummaryAdmin", zone)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ShippingZone_DefaultTags_SummaryAdmin", zone)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Covers:5"),
            View("ShippingZone_Methods_SummaryAdmin", zone)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Methods:5"),
            View("ShippingZone_Buttons_SummaryAdmin", zone).Location("Actions:5")
        );
    }

    public override IDisplayResult Edit(ShippingZone zone, BuildEditorContext context)
        => Combine(
            Initialize<ShippingZoneViewModel>("ShippingZoneFields_Edit", async model =>
            {
                model.IsNew = context.IsNew;
                model.Name = zone.Name;
                model.Position = zone.Position;
                model.RestOfWorld = zone.RestOfWorld;
                model.Countries = [.. zone.Countries];
                model.Regions = [.. zone.Regions];

                var countries = await _countryService.GetCountriesAsync();

                model.AllCountries = countries
                    .Select(country => new SelectListItem(country.Name, country.Code, zone.Countries.Contains(country.Code, StringComparer.OrdinalIgnoreCase)))
                    .ToArray();

                // Each chosen country with regions can be narrowed to some of them.
                var countryRegions = new List<ShippingZoneCountryRegions>();

                foreach (var code in zone.Countries)
                {
                    var format = await _formatProvider.GetFormatAsync(code);

                    if (format?.Regions is not { Count: > 0 } regions)
                    {
                        continue;
                    }

                    countryRegions.Add(new ShippingZoneCountryRegions
                    {
                        CountryCode = code,
                        CountryName = countries.FirstOrDefault(country => string.Equals(country.Code, code, StringComparison.OrdinalIgnoreCase))?.Name ?? code,
                        Regions = regions
                            .Select(region => (Value: $"{code}-{region.Code}", region.Name))
                            .Select(region => new SelectListItem(region.Name, region.Value, zone.Regions.Contains(region.Value, StringComparer.OrdinalIgnoreCase)))
                            .ToArray(),
                    });
                }

                model.CountryRegions = countryRegions;
            }).Location("Content:1"),
            Initialize<ShippingZoneMethodsViewModel>("ShippingZoneMethods_Edit", model =>
            {
                model.Zone = zone;
                model.Providers = [.. _providers];
                model.Methods = zone.Methods
                    .OrderBy(method => method.Position)
                    .ThenBy(method => method.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(method => new ShippingZoneMethodRow
                    {
                        Method = method,
                        ProviderName = _providers.FirstOrDefault(provider => string.Equals(provider.Name, method.ProviderName, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? method.ProviderName,
                        Summary = Summarize(method),
                    })
                    .ToArray();
            }).Location("Content:10")
            .RenderWhen(() => Task.FromResult(!context.IsNew)));

    public override async Task<IDisplayResult> UpdateAsync(ShippingZone zone, UpdateEditorContext context)
    {
        var model = new ShippingZoneViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix,
            viewModel => viewModel.Name,
            viewModel => viewModel.Position,
            viewModel => viewModel.RestOfWorld,
            viewModel => viewModel.Countries,
            viewModel => viewModel.Regions);

        zone.Name = model.Name?.Trim();
        zone.Position = model.Position;
        zone.RestOfWorld = model.RestOfWorld;
        zone.Countries = ShippingZoneHandler.Normalize(model.Countries);

        // A region of a country no longer in the zone would narrow nothing.
        zone.Regions = ShippingZoneHandler.Normalize(model.Regions)
            .Where(region => zone.Countries.Any(country => region.StartsWith(country + "-", StringComparison.Ordinal)))
            .ToList();

        return Edit(zone, context);
    }

    private string Summarize(ShippingMethod method)
        => method.ProviderName switch
        {
            ShippingConstants.Providers.Flat when method.TryGet<FlatRateSettings>(out var flat) => flat.PerItemAmount > 0
                ? S["{0} per order, plus {1} per item", flat.Amount, flat.PerItemAmount]
                : S["{0} per order", flat.Amount],
            ShippingConstants.Providers.Table when method.TryGet<TableRateSettings>(out var table) => table.Basis switch
            {
                TableRateBasis.Subtotal => S["By subtotal, {0} rows", table.Rows.Count],
                TableRateBasis.Quantity => S["By number of items, {0} rows", table.Rows.Count],
                _ => S["By weight, {0} rows", table.Rows.Count],
            },
            ShippingConstants.Providers.Free when method.TryGet<FreeShippingSettings>(out var free) => free.MinimumSubtotal > 0
                ? S["Free from a subtotal of {0}", free.MinimumSubtotal]
                : S["Always free"],
            _ => string.Empty,
        };
}
