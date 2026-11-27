using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Assets.Core.Indexes;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Fleet;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Assets.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using YesSql;
using Asset = CrestApps.OrchardCore.Assets.Models.Asset;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Assets.Drivers;

/// <summary>
/// The main section of the asset editor: details, owner and place, installation and warranty, the values the asset's
/// type records, the fleet details and the documents. Other modules add their own sections to the editor with display
/// drivers of <see cref="Asset"/>.
/// </summary>
internal sealed class AssetDisplayDriver : DisplayDriver<Asset>
{
    private static readonly AssetCategory[] _fleetCategories = [AssetCategory.Vehicle, AssetCategory.Equipment, AssetCategory.Tool];

    private readonly AssetTypeManager _typeManager;
    private readonly AssetOwnerDirectory _owners;
    private readonly ISession _session;
    private readonly bool _fleet;

    internal readonly IStringLocalizer S;

    public AssetDisplayDriver(
        AssetTypeManager typeManager,
        AssetOwnerDirectory owners,
        ISession session,
        IEnumerable<FleetFeature> fleet,
        IStringLocalizer<AssetDisplayDriver> stringLocalizer)
    {
        _typeManager = typeManager;
        _owners = owners;
        _session = session;
        _fleet = fleet.Any();
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(Asset asset, BuildEditorContext context)
    {
        // The editor is one shape for each card, each a copy of the asset's values, so every card renders its own rows
        // while the fields keep posting under the same prefix. Each shape reads only what its card shows.
        return Combine(
            Initialize<AssetEditViewModel>("AssetFields_Edit", async model =>
            {
                FillValues(model, asset, context);
                await FillTypeAsync(model);
            }).Location(l => l.Zone("Content", "1").Card(S["Details"], "10")),
            Initialize<AssetEditViewModel>("AssetOwner_Edit", async model =>
            {
                FillValues(model, asset, context);
                await FillOwnerAsync(model);
            }).Location(l => l.Zone("Content", "2").Card(S["Owner and Place"], "20")),
            Initialize<AssetEditViewModel>("AssetWarranty_Edit", model => FillValues(model, asset, context))
                .Location(l => l.Zone("Content", "3").Card(S["Installation and Warranty"], "30")),
            Initialize<AssetEditViewModel>("AssetAttributes_Edit", async model =>
            {
                FillValues(model, asset, context);
                await FillTypeAsync(model);
            }).Location(l => l.Zone("Content", "4").Card(S["Type Details"], "40")),
            Initialize<AssetEditViewModel>("AssetDocuments_Edit", model => FillValues(model, asset, context))
                .Location(l => l.Zone("Content", "5").Card(S["Documents"], "50")),
            Initialize<AssetEditViewModel>("AssetVehicle_Edit", async model =>
            {
                FillValues(model, asset, context);
                await FillTypeAsync(model);
            }).Location(l => l.Zone("Content", "6").Card(S["Vehicle"], "55")),
            Initialize<AssetEditViewModel>("AssetFleet_Edit", async model =>
            {
                FillValues(model, asset, context);
                await FillTypeAsync(model);
            }).Location(l => l.Zone("Content", "6").Card(S["Fleet"], "60")));
    }

    private static void FillValues(AssetEditViewModel model, Asset asset, BuildEditorContext context)
    {
        model.AssetId = context.IsNew ? null : asset.ItemId;
        model.AssetTypeId = asset.AssetTypeId;
        model.Number = asset.Number;
        model.Name = asset.Name;
        model.SerialNumber = asset.SerialNumber;
        model.Manufacturer = asset.Manufacturer;
        model.Model = asset.Model;
        model.Status = asset.Status;
        model.OwnerKind = asset.OwnerKind;
        model.OwnerId = asset.OwnerId;
        model.ParentAssetId = asset.ParentAssetId;
        model.InstallDate = asset.InstallDate;
        model.AddressLine1 = asset.Location?.AddressLine1;
        model.City = asset.Location?.City;
        model.Region = asset.Location?.Region;
        model.PostalCode = asset.Location?.PostalCode;
        model.Country = asset.Location?.Country;
        model.WarrantyStart = asset.Warranty?.StartDate;
        model.WarrantyEnd = asset.Warranty?.EndDate;
        model.WarrantyProvider = asset.Warranty?.Provider;
        model.AttributeValues = new Dictionary<string, string>(asset.Attributes ?? [], StringComparer.OrdinalIgnoreCase);
        model.Documents = string.Join(Environment.NewLine, asset.Documents ?? []);
        model.Schedulable = asset.Fleet?.Schedulable ?? false;
        model.CarriesStock = asset.Fleet?.CarriesStock ?? false;
        model.Plate = asset.Fleet?.Plate;
        model.Vin = asset.Fleet?.Vin;
        model.Capacity = asset.Fleet?.Capacity;
        model.FuelType = asset.Fleet?.FuelType;
    }

    private async Task FillTypeAsync(AssetEditViewModel model)
    {
        var types = (await _typeManager.GetAllAsync()).OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

        model.Type = types.FirstOrDefault(type => type.ItemId == model.AssetTypeId);
        model.TypeOptions = [.. types.Select(type => new SelectListItem(type.Name, type.ItemId, type.ItemId == model.AssetTypeId))];
        model.ShowFleet = _fleet && model.Type is not null && _fleetCategories.Contains(model.Type.Category);
        model.ShowVehicle = HasVehicleDetails(model.Type);
    }

    private async Task FillOwnerAsync(AssetEditViewModel model)
    {
        model.ParentOptions = [.. (await _session.QueryIndex<AssetIndex>(index => index.ItemId != model.AssetId, collection: AssetsConstants.CollectionName).ListAsync())
            .Where(index => index.ItemId != model.AssetId)
            .OrderBy(index => index.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(index => new SelectListItem($"{index.Name} ({index.Number})", index.ItemId, index.ItemId == model.ParentAssetId))];
        model.OwnerKinds = [.. _owners.Kinds.Union(model.OwnerKind == AssetOwnerKind.Internal ? [] : [model.OwnerKind])];
        model.OwnerName = model.OwnerKind == AssetOwnerKind.Internal ? null : await _owners.GetNameAsync(model.OwnerKind, model.OwnerId) ?? model.OwnerId;
    }

    public override async Task<IDisplayResult> UpdateAsync(Asset asset, UpdateEditorContext context)
    {
        var model = new AssetEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var type = string.IsNullOrEmpty(model.AssetTypeId) ? null : await _typeManager.FindByIdAsync(model.AssetTypeId);

        asset.AssetTypeId = type?.ItemId;
        asset.Number = Blank(model.Number);
        asset.Name = model.Name?.Trim();
        asset.SerialNumber = Blank(model.SerialNumber);
        asset.Manufacturer = Blank(model.Manufacturer);
        asset.Model = Blank(model.Model);
        asset.Status = model.Status;
        asset.OwnerKind = model.OwnerKind;
        asset.OwnerId = model.OwnerKind == AssetOwnerKind.Internal ? null : Blank(model.OwnerIds?.GetValueOrDefault(model.OwnerKind.ToString()) ?? model.OwnerId);
        asset.ParentAssetId = Blank(model.ParentAssetId);
        asset.InstallDate = model.InstallDate;
        asset.Location = MakeAddress(model, asset.Location);
        asset.Warranty = new AssetWarranty { StartDate = model.WarrantyStart, EndDate = model.WarrantyEnd, Provider = Blank(model.WarrantyProvider), Terms = asset.Warranty?.Terms };
        asset.Attributes = new Dictionary<string, string>(
            (type?.Attributes ?? []).Select(attribute => (attribute.Name, Value: model.AttributeValues?.GetValueOrDefault(attribute.Name))).Where(entry => !string.IsNullOrWhiteSpace(entry.Value)).Select(entry => KeyValuePair.Create(entry.Name, entry.Value.Trim())),
            StringComparer.OrdinalIgnoreCase);
        asset.Documents = [.. (model.Documents ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

        // A vehicle's plate and VIN are kept with or without Fleet; booking, stock, capacity and fuel are Fleet's. Both are
        // for the business's own vehicles and equipment.
        if (HasVehicleDetails(type) && model.OwnerKind == AssetOwnerKind.Internal)
        {
            asset.Fleet ??= new AssetFleet();
            asset.Fleet.Plate = Blank(model.Plate);
            asset.Fleet.Vin = Blank(model.Vin);
        }

        if (_fleet && type is not null && _fleetCategories.Contains(type.Category) && model.OwnerKind == AssetOwnerKind.Internal)
        {
            asset.Fleet ??= new AssetFleet();
            asset.Fleet.Schedulable = model.Schedulable;
            asset.Fleet.CarriesStock = model.CarriesStock;
            asset.Fleet.Capacity = Blank(model.Capacity);
            asset.Fleet.FuelType = Blank(model.FuelType);
        }

        return Edit(asset, context);
    }

    private static Address MakeAddress(AssetEditViewModel model, Address existing)
    {
        if (string.IsNullOrWhiteSpace(model.AddressLine1) && string.IsNullOrWhiteSpace(model.City) && string.IsNullOrWhiteSpace(model.PostalCode))
        {
            return null;
        }

        // A moved address loses its old coordinates.
        var same = existing is not null && existing.AddressLine1 == Blank(model.AddressLine1) && existing.City == Blank(model.City) && existing.PostalCode == Blank(model.PostalCode) && existing.Country == Blank(model.Country);

        return new Address
        {
            AddressLine1 = Blank(model.AddressLine1),
            City = Blank(model.City),
            Region = Blank(model.Region),
            PostalCode = Blank(model.PostalCode),
            Country = Blank(model.Country)?.ToUpperInvariant(),
            Latitude = same ? existing.Latitude : null,
            Longitude = same ? existing.Longitude : null,
            GeocodedUtc = same ? existing.GeocodedUtc : null,
            GeocodeSource = same ? existing.GeocodeSource : null,
        };
    }

    private bool HasVehicleDetails(AssetType type)
        => type is not null && (type.Category == AssetCategory.Vehicle || (_fleet && _fleetCategories.Contains(type.Category)));

    private static string Blank(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
