using CrestApps.OrchardCore.Assets.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using Asset = CrestApps.OrchardCore.Assets.Models.Asset;

namespace CrestApps.OrchardCore.Assets.ViewModels;

/// <summary>
/// A row of the asset type list.
/// </summary>
public sealed class AssetTypeRow
{
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public AssetType Type { get; set; }

    /// <summary>
    /// Gets or sets how many assets are of the type.
    /// </summary>
    public int AssetCount { get; set; }
}

/// <summary>
/// A row of the asset list.
/// </summary>
public sealed class AssetRow
{
    /// <summary>
    /// Gets or sets the asset.
    /// </summary>
    public Asset Asset { get; set; }

    /// <summary>
    /// Gets or sets its type's name.
    /// </summary>
    public string TypeName { get; set; }

    /// <summary>
    /// Gets or sets the name of the asset it is part of.
    /// </summary>
    public string ParentName { get; set; }

    /// <summary>
    /// Gets or sets today, for the warranty.
    /// </summary>
    public DateOnly Today { get; set; }
}

/// <summary>
/// A list page of asset types or assets.
/// </summary>
public class AssetsListViewModel
{
    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string Q { get; set; }

    /// <summary>
    /// Gets or sets the type filter.
    /// </summary>
    public string TypeId { get; set; }

    /// <summary>
    /// Gets or sets the state filter.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the types.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TypeOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the list.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }
}

/// <summary>
/// The asset type editor.
/// </summary>
public class AssetTypeEditViewModel
{
    /// <summary>
    /// Gets or sets the identifier, or <see langword="null"/> for a new type.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the category.
    /// </summary>
    public AssetCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the attributes; rows without a name are left out.
    /// </summary>
    public List<AssetAttributeEditModel> Attributes { get; set; } = [];

    /// <summary>
    /// Gets or sets the meters; rows without a name are left out.
    /// </summary>
    public List<MeterEditModel> Meters { get; set; } = [];
}

/// <summary>
/// An attribute row of the asset type editor.
/// </summary>
public class AssetAttributeEditModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the kind of value.
    /// </summary>
    public AssetAttributeKind Kind { get; set; }
}

/// <summary>
/// A meter row of the asset type editor.
/// </summary>
public class MeterEditModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the unit.
    /// </summary>
    public string Unit { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it only goes up.
    /// </summary>
    public bool Cumulative { get; set; } = true;
}

/// <summary>
/// The asset editor.
/// </summary>
public class AssetEditViewModel
{
    /// <summary>
    /// Gets or sets the identifier, or <see langword="null"/> for a new asset.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string AssetTypeId { get; set; }

    /// <summary>
    /// Gets or sets the asset number, or empty for the next one.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the serial number.
    /// </summary>
    public string SerialNumber { get; set; }

    /// <summary>
    /// Gets or sets the manufacturer.
    /// </summary>
    public string Manufacturer { get; set; }

    /// <summary>
    /// Gets or sets the model.
    /// </summary>
    public string Model { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public AssetStatus Status { get; set; }

    /// <summary>
    /// Gets or sets who owns it.
    /// </summary>
    public AssetOwnerKind OwnerKind { get; set; }

    /// <summary>
    /// Gets or sets the customer or company that owns it.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the asset it is part of.
    /// </summary>
    public string ParentAssetId { get; set; }

    /// <summary>
    /// Gets or sets when it was installed.
    /// </summary>
    public DateOnly? InstallDate { get; set; }

    /// <summary>
    /// Gets or sets the street address.
    /// </summary>
    public string AddressLine1 { get; set; }

    /// <summary>
    /// Gets or sets the city.
    /// </summary>
    public string City { get; set; }

    /// <summary>
    /// Gets or sets the region or state.
    /// </summary>
    public string Region { get; set; }

    /// <summary>
    /// Gets or sets the postal code.
    /// </summary>
    public string PostalCode { get; set; }

    /// <summary>
    /// Gets or sets the two-letter country code.
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Gets or sets the warranty's first day.
    /// </summary>
    public DateOnly? WarrantyStart { get; set; }

    /// <summary>
    /// Gets or sets the warranty's last day.
    /// </summary>
    public DateOnly? WarrantyEnd { get; set; }

    /// <summary>
    /// Gets or sets who gives the warranty.
    /// </summary>
    public string WarrantyProvider { get; set; }

    /// <summary>
    /// Gets or sets the attribute values, by name.
    /// </summary>
    public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the documents, one media path a line.
    /// </summary>
    public string Documents { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it is booked for visits.
    /// </summary>
    public bool Schedulable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it carries stock.
    /// </summary>
    public bool CarriesStock { get; set; }

    /// <summary>
    /// Gets or sets the registration plate.
    /// </summary>
    public string Plate { get; set; }

    /// <summary>
    /// Gets or sets the vehicle identification number.
    /// </summary>
    public string Vin { get; set; }

    /// <summary>
    /// Gets or sets what it carries.
    /// </summary>
    public string Capacity { get; set; }

    /// <summary>
    /// Gets or sets the fuel type.
    /// </summary>
    public string FuelType { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    [BindNever]
    public AssetType Type { get; set; }

    /// <summary>
    /// Gets or sets the types.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TypeOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the assets it can be part of.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> ParentOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the fleet section shows.
    /// </summary>
    [BindNever]
    public bool ShowFleet { get; set; }
}

/// <summary>
/// An asset's page.
/// </summary>
public class AssetDetailsViewModel
{
    /// <summary>
    /// Gets or sets the asset.
    /// </summary>
    [BindNever]
    public Asset Asset { get; set; }

    /// <summary>
    /// Gets or sets its type.
    /// </summary>
    [BindNever]
    public AssetType Type { get; set; }

    /// <summary>
    /// Gets or sets the assets it is inside, from the top down.
    /// </summary>
    [BindNever]
    public IReadOnlyList<Asset> Ancestors { get; set; } = [];

    /// <summary>
    /// Gets or sets the assets inside it.
    /// </summary>
    [BindNever]
    public IReadOnlyList<Asset> Children { get; set; } = [];

    /// <summary>
    /// Gets or sets its meter readings, newest first.
    /// </summary>
    [BindNever]
    public IReadOnlyList<AssetMeterReading> Readings { get; set; } = [];

    /// <summary>
    /// Gets or sets today.
    /// </summary>
    [BindNever]
    public DateOnly Today { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user can change it.
    /// </summary>
    [BindNever]
    public bool CanManage { get; set; }

    /// <summary>
    /// Gets or sets the meter of a new reading.
    /// </summary>
    public string Meter { get; set; }

    /// <summary>
    /// Gets or sets the value of a new reading.
    /// </summary>
    public decimal? Value { get; set; }
}
