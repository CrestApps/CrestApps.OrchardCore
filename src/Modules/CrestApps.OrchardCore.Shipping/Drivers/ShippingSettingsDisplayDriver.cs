using CrestApps.Core;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping.Models;
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
/// Settings › Commerce › Shipping.
/// </summary>
public sealed class ShippingSettingsDisplayDriver : SiteDisplayDriver<ShippingSettings>
{
    private const int MaxClasses = 50;
    private const int MaxPickupLocations = 20;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<IFulfillmentLocationProvider> _fulfillmentLocations;

    internal readonly IStringLocalizer S;

    public ShippingSettingsDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IEnumerable<IFulfillmentLocationProvider> fulfillmentLocations,
        IStringLocalizer<ShippingSettingsDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _fulfillmentLocations = fulfillmentLocations;
        S = stringLocalizer;
    }

    protected override string SettingsGroupId
        => ShippingConstants.SettingsGroupId;

    // The settings in three tabs, after the carrier providers': General, Store pickup and Sending parcels. One model
    // binds them all, so each tab's shape is filled the same way.
    public override IDisplayResult Edit(ISite site, ShippingSettings settings, BuildEditorContext context)
        => Combine(
            Tab("ShippingSettings_Edit", "Content:10#General", settings),
            Tab("ShippingPickupSettings_Edit", "Content:20#Store pickup", settings),
            Tab("ShippingSenderSettings_Edit", "Content:30#Sending parcels", settings));

    private ShapeResult Tab(string shapeType, string location, ShippingSettings settings)
        => Initialize<ShippingSettingsViewModel>(shapeType, model => PopulateAsync(model, settings)).Location(location)
            .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ShippingPermissions.ManageShipping))
            .OnGroup(SettingsGroupId);

    private async ValueTask PopulateAsync(ShippingSettingsViewModel model, ShippingSettings settings)
    {
        model.WeightUnit = settings.WeightUnit;
        model.ShippingClasses = string.Join(Environment.NewLine, settings.ShippingClasses ?? []);
        model.Carriers = string.Join(Environment.NewLine, (settings.Carriers ?? []).Select(carrier => string.IsNullOrEmpty(carrier.TrackingUrlTemplate) ? carrier.Name : $"{carrier.Name} | {carrier.TrackingUrlTemplate}"));
        model.SendShippedEmails = settings.SendShippedEmails;
        model.PickupLocations = (settings.PickupLocations ?? [])
            .Select(location => new PickupLocationViewModel
            {
                LocationId = location.LocationId,
                Name = location.Name,
                AddressLine1 = location.AddressLine1,
                City = location.City,
                Region = location.Region,
                PostalCode = location.PostalCode,
                Country = location.Country,
                Instructions = location.Instructions,
                Fee = location.Fee,
                StockLocationId = location.StockLocationId,
            })
            .ToList();
        model.StockLocations = await GetStockLocationsAsync();

        var from = settings.ShipFrom;

        model.ShipFromName = from?.Name;
        model.ShipFromCompany = from?.Company;
        model.ShipFromAddressLine1 = from?.AddressLine1;
        model.ShipFromAddressLine2 = from?.AddressLine2;
        model.ShipFromCity = from?.City;
        model.ShipFromRegion = from?.Region;
        model.ShipFromPostalCode = from?.PostalCode;
        model.ShipFromCountry = from?.Country;
        model.ShipFromPhone = from?.Phone;
        model.DefaultParcelLength = settings.DefaultParcelLength;
        model.DefaultParcelWidth = settings.DefaultParcelWidth;
        model.DefaultParcelHeight = settings.DefaultParcelHeight;
        model.PackagingWeight = settings.PackagingWeight;

        // A blank row to add a location with, while there is room for one.
        if (model.PickupLocations.Count < MaxPickupLocations)
        {
            model.PickupLocations.Add(new PickupLocationViewModel());
        }
    }

    public override async Task<IDisplayResult> UpdateAsync(ISite site, ShippingSettings settings, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ShippingPermissions.ManageShipping))
        {
            return null;
        }

        var model = new ShippingSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        settings.WeightUnit = Enum.IsDefined(model.WeightUnit) ? model.WeightUnit : WeightUnit.Kilogram;
        settings.ShippingClasses = ParseClasses(model.ShippingClasses);
        settings.Carriers = ParseCarriers(model.Carriers);
        settings.SendShippedEmails = model.SendShippedEmails;
        settings.PickupLocations = ParsePickupLocations(model.PickupLocations, context);

        // Where parcels leave from: kept once it has a street, a city and a country.
        settings.ShipFrom = string.IsNullOrWhiteSpace(model.ShipFromAddressLine1) && string.IsNullOrWhiteSpace(model.ShipFromCity)
            ? null
            : new CrestApps.OrchardCore.Addresses.Models.Address
            {
                Name = Trim(model.ShipFromName),
                Company = Trim(model.ShipFromCompany),
                AddressLine1 = Trim(model.ShipFromAddressLine1),
                AddressLine2 = Trim(model.ShipFromAddressLine2),
                City = Trim(model.ShipFromCity),
                Region = Trim(model.ShipFromRegion),
                PostalCode = Trim(model.ShipFromPostalCode),
                Country = Trim(model.ShipFromCountry)?.ToUpperInvariant(),
                Phone = Trim(model.ShipFromPhone),
            };

        if (settings.ShipFrom is { } shipFrom && (string.IsNullOrEmpty(shipFrom.AddressLine1) || string.IsNullOrEmpty(shipFrom.City) || string.IsNullOrEmpty(shipFrom.Country)))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ShipFromAddressLine1), S["Enter the street, city and country parcels are sent from."]);
        }

        settings.DefaultParcelLength = model.DefaultParcelLength is > 0 ? model.DefaultParcelLength : null;
        settings.DefaultParcelWidth = model.DefaultParcelWidth is > 0 ? model.DefaultParcelWidth : null;
        settings.DefaultParcelHeight = model.DefaultParcelHeight is > 0 ? model.DefaultParcelHeight : null;
        settings.PackagingWeight = Math.Max(0, model.PackagingWeight);

        return Edit(site, settings, context);
    }

    // A row left blank, such as the one to add a location with, or marked for removal is dropped; a row with a name
    // needs the address buyers come to.
    private List<PickupLocation> ParsePickupLocations(List<PickupLocationViewModel> rows, UpdateEditorContext context)
    {
        var locations = new List<PickupLocation>();

        for (var i = 0; i < (rows?.Count ?? 0); i++)
        {
            var row = rows[i];

            if (row is null || row.Remove || IsBlank(row))
            {
                continue;
            }

            var prefix = $"{nameof(ShippingSettingsViewModel.PickupLocations)}[{i}].";

            if (string.IsNullOrWhiteSpace(row.Name))
            {
                context.Updater.ModelState.AddModelError(Prefix, prefix + nameof(row.Name), S["Give the pickup location a name buyers recognize, such as Downtown store."]);
            }

            if (string.IsNullOrWhiteSpace(row.AddressLine1) || string.IsNullOrWhiteSpace(row.City) || string.IsNullOrWhiteSpace(row.Country))
            {
                context.Updater.ModelState.AddModelError(Prefix, prefix + nameof(row.AddressLine1), S["Enter the street, city and country of {0}.", row.Name?.Trim() ?? S["the pickup location"]]);
            }

            if (row.Fee < 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, prefix + nameof(row.Fee), S["A pickup fee can't be negative."]);
            }

            locations.Add(new PickupLocation
            {
                LocationId = string.IsNullOrWhiteSpace(row.LocationId) ? UniqueId.GenerateId() : row.LocationId.Trim(),
                Name = row.Name?.Trim(),
                AddressLine1 = row.AddressLine1?.Trim(),
                City = row.City?.Trim(),
                Region = Trim(row.Region),
                PostalCode = Trim(row.PostalCode),
                Country = row.Country?.Trim().ToUpperInvariant(),
                Instructions = Trim(row.Instructions),
                Fee = Math.Max(0, row.Fee),
                StockLocationId = Trim(row.StockLocationId),
            });
        }

        return locations.DistinctBy(location => location.LocationId, StringComparer.Ordinal).Take(MaxPickupLocations).ToList();
    }

    private async Task<IReadOnlyList<SelectListItem>> GetStockLocationsAsync()
    {
        var items = new List<SelectListItem>();

        foreach (var provider in _fulfillmentLocations)
        {
            items.AddRange((await provider.GetLocationsAsync()).Select(location => new SelectListItem(location.Name, location.LocationId)));
        }

        return items;
    }

    private static bool IsBlank(PickupLocationViewModel row)
        => string.IsNullOrWhiteSpace(row.Name) && string.IsNullOrWhiteSpace(row.AddressLine1) && string.IsNullOrWhiteSpace(row.City) &&
            string.IsNullOrWhiteSpace(row.PostalCode) && string.IsNullOrWhiteSpace(row.Instructions);

    private static string Trim(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // One carrier per line: its name, then optionally "|" and its tracking link with {0} for the number.
    internal static List<ShippingCarrier> ParseCarriers(string text)
        => (text ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('|', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts[0].Length > 0)
            .Select(parts => new ShippingCarrier
            {
                Name = parts[0],
                TrackingUrlTemplate = parts.Length > 1 && IsWebLink(parts[1]) ? parts[1] : null,
            })
            .DistinctBy(carrier => carrier.Name, StringComparer.OrdinalIgnoreCase)
            .Take(MaxClasses)
            .ToList();

    private static bool IsWebLink(string value)
        => Uri.TryCreate(value.Replace("{0}", "0", StringComparison.Ordinal), UriKind.Absolute, out var url) && url.Scheme is "http" or "https";

    internal static List<string> ParseClasses(string text)
        => (text ?? string.Empty)
            .Split(['\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxClasses)
            .ToList();
}
