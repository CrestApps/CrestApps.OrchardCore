using CrestApps.Core.Models;
using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Assets.Models;

/// <summary>
/// Who owns an asset.
/// </summary>
public enum AssetOwnerKind
{
    /// <summary>
    /// The business itself, such as its vans and tools.
    /// </summary>
    Internal,

    /// <summary>
    /// A customer: their installed base.
    /// </summary>
    Customer,

    /// <summary>
    /// A company customer.
    /// </summary>
    Company,
}

/// <summary>
/// The state of an asset.
/// </summary>
public enum AssetStatus
{
    /// <summary>
    /// In use.
    /// </summary>
    Active,

    /// <summary>
    /// Being serviced.
    /// </summary>
    InService,

    /// <summary>
    /// Out of service.
    /// </summary>
    OutOfService,

    /// <summary>
    /// No longer used.
    /// </summary>
    Retired,
}

/// <summary>
/// A warranty.
/// </summary>
public sealed class AssetWarranty
{
    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the last day.
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// Gets or sets who gives it.
    /// </summary>
    public string Provider { get; set; }

    /// <summary>
    /// Gets or sets its terms.
    /// </summary>
    public string Terms { get; set; }

    /// <summary>
    /// Tells whether the warranty covers a day.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public bool Covers(DateOnly date) => (StartDate is null || StartDate <= date) && EndDate is not null && date <= EndDate;
}

/// <summary>
/// What the Fleet feature keeps about a vehicle, equipment or tool the business owns.
/// </summary>
public sealed class AssetFleet
{
    /// <summary>
    /// Gets or sets a value indicating whether it is booked for visits, with its own Equipment calendar that refuses double-booking.
    /// </summary>
    public bool Schedulable { get; set; }

    /// <summary>
    /// Gets or sets its Equipment calendar.
    /// </summary>
    public string CalendarId { get; set; }

    /// <summary>
    /// Gets or sets the registration plate.
    /// </summary>
    public string Plate { get; set; }

    /// <summary>
    /// Gets or sets the vehicle identification number.
    /// </summary>
    public string Vin { get; set; }

    /// <summary>
    /// Gets or sets what it carries, such as 2 people and 800 kg.
    /// </summary>
    public string Capacity { get; set; }

    /// <summary>
    /// Gets or sets the fuel type.
    /// </summary>
    public string FuelType { get; set; }

    /// <summary>
    /// Gets or sets where it is kept overnight.
    /// </summary>
    public Address HomeBase { get; set; }

    /// <summary>
    /// Gets or sets the worker who drives it by default.
    /// </summary>
    public string DefaultDriverWorkerId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it carries stock, kept at its own Inventory location.
    /// </summary>
    public bool CarriesStock { get; set; }

    /// <summary>
    /// Gets or sets its Inventory location.
    /// </summary>
    public string StockLocationId { get; set; }
}

/// <summary>
/// A piece of equipment, a vehicle, a tool or a unit installed at a customer.
/// </summary>
public sealed class Asset : CatalogItem
{
    /// <summary>
    /// Gets or sets the asset number, such as A-00042.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string AssetTypeId { get; set; }

    /// <summary>
    /// Gets or sets the product it is, or <see langword="null"/>.
    /// </summary>
    public string ProductId { get; set; }

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
    /// Gets or sets who owns it.
    /// </summary>
    public AssetOwnerKind OwnerKind { get; set; }

    /// <summary>
    /// Gets or sets the customer or company that owns it.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets where it is, with coordinates once geocoded.
    /// </summary>
    public Address Location { get; set; }

    /// <summary>
    /// Gets or sets the asset it is part of or installed in.
    /// </summary>
    public string ParentAssetId { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public AssetStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when it was installed.
    /// </summary>
    public DateOnly? InstallDate { get; set; }

    /// <summary>
    /// Gets or sets the warranty.
    /// </summary>
    public AssetWarranty Warranty { get; set; } = new();

    /// <summary>
    /// Gets or sets the values of its type's attributes, by name.
    /// </summary>
    public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the paths of its documents in the media library.
    /// </summary>
    public List<string> Documents { get; set; } = [];

    /// <summary>
    /// Gets or sets its fleet data, for vehicles, equipment and tools the business owns.
    /// </summary>
    public AssetFleet Fleet { get; set; }

    /// <summary>
    /// Gets or sets when it was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
