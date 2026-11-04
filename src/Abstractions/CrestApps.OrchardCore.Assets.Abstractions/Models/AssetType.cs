using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Assets.Models;

/// <summary>
/// What kind of thing an asset type describes.
/// </summary>
public enum AssetCategory
{
    /// <summary>
    /// Equipment, such as a boiler or a forklift.
    /// </summary>
    Equipment,

    /// <summary>
    /// A vehicle, such as a service van.
    /// </summary>
    Vehicle,

    /// <summary>
    /// A tool, such as a ladder or a meter.
    /// </summary>
    Tool,

    /// <summary>
    /// A unit installed at a customer, such as an air conditioner.
    /// </summary>
    CustomerUnit,

    /// <summary>
    /// A part of another asset, such as a compressor.
    /// </summary>
    Component,

    /// <summary>
    /// A building or room.
    /// </summary>
    Facility,
}

/// <summary>
/// The kind of value an asset attribute holds.
/// </summary>
public enum AssetAttributeKind
{
    /// <summary>
    /// Text.
    /// </summary>
    Text,

    /// <summary>
    /// A number.
    /// </summary>
    Number,

    /// <summary>
    /// A date.
    /// </summary>
    Date,

    /// <summary>
    /// Yes or no.
    /// </summary>
    YesNo,
}

/// <summary>
/// A value every asset of a type records, such as a refrigerant or a capacity.
/// </summary>
public sealed class AssetAttributeDefinition
{
    /// <summary>
    /// Gets or sets the name, used as the key of the value.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the kind of value.
    /// </summary>
    public AssetAttributeKind Kind { get; set; }
}

/// <summary>
/// A meter assets of a type have, such as hours run or miles.
/// </summary>
public sealed class MeterDefinition
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the unit, such as hours or miles.
    /// </summary>
    public string Unit { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the meter only goes up, so a lower reading is refused.
    /// </summary>
    public bool Cumulative { get; set; } = true;
}

/// <summary>
/// A kind of asset: its category, the attributes it records and its meters.
/// </summary>
public sealed class AssetType : CatalogItem
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the category.
    /// </summary>
    public AssetCategory Category { get; set; }

    /// <summary>
    /// Gets or sets a description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the attributes.
    /// </summary>
    public List<AssetAttributeDefinition> Attributes { get; set; } = [];

    /// <summary>
    /// Gets or sets the meters.
    /// </summary>
    public List<MeterDefinition> Meters { get; set; } = [];

    /// <summary>
    /// Gets or sets when it was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
