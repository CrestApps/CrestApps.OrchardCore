using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Assets.Models;

/// <summary>
/// Where a meter reading came from.
/// </summary>
public enum MeterReadingSource
{
    /// <summary>
    /// Entered by hand.
    /// </summary>
    Manual,

    /// <summary>
    /// Recorded on a work order.
    /// </summary>
    WorkOrder,

    /// <summary>
    /// Sent by another system.
    /// </summary>
    Api,
}

/// <summary>
/// A reading of an asset's meter. Readings are only added, never changed.
/// </summary>
public sealed class AssetMeterReading : CatalogItem
{
    /// <summary>
    /// Gets or sets the asset.
    /// </summary>
    public string AssetId { get; set; }

    /// <summary>
    /// Gets or sets the meter, by name.
    /// </summary>
    public string Meter { get; set; }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>
    /// Gets or sets when it was read.
    /// </summary>
    public DateTime Utc { get; set; }

    /// <summary>
    /// Gets or sets where it came from.
    /// </summary>
    public MeterReadingSource Source { get; set; }

    /// <summary>
    /// Gets or sets the record it came from, such as a work order.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets who recorded it.
    /// </summary>
    public string RecordedBy { get; set; }
}
