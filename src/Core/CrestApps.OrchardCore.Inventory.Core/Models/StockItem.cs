using System.Text.Json.Serialization;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Inventory.Core.Models;

/// <summary>
/// How many units of one product, or one of its variants, a location holds. The counts are a cache of the stock
/// movements, which can rebuild them.
/// </summary>
public sealed class StockItem : CatalogItem
{
    public long Id { get; set; }

    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the variant counted, or <see langword="null"/> for a product without variants.
    /// </summary>
    public string VariantId { get; set; }

    public string LocationId { get; set; } = InventoryConstants.DefaultLocationId;

    /// <summary>
    /// Gets or sets the SKU, copied from the product or variant, for search, import and export.
    /// </summary>
    public string Sku { get; set; }

    /// <summary>
    /// Gets or sets the product's title, with the variant's after it, as the lists show it.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the units the location holds.
    /// </summary>
    public int OnHand { get; set; }

    /// <summary>
    /// Gets or sets the units held for orders that are being paid for.
    /// </summary>
    public int Reserved { get; set; }

    /// <summary>
    /// Gets the units that can still be sold.
    /// </summary>
    [JsonIgnore]
    public int Available => OnHand - Reserved;

    /// <summary>
    /// Gets or sets how few available units count as low stock: the product's own threshold, or the site's default.
    /// </summary>
    public int LowStockThreshold { get; set; }

    /// <summary>
    /// Gets or sets whether the product may be sold when none is left.
    /// </summary>
    public bool AllowBackorder { get; set; }

    /// <summary>
    /// Gets or sets when the product ships, from its inventory settings, which makes a backorder a pre-order until then.
    /// </summary>
    public DateTime? ReleaseDateUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the product or variant counted is gone or no longer tracked. Its history is kept.
    /// </summary>
    public bool Archived { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }

    [JsonIgnore]
    public bool IsLow => Available <= LowStockThreshold;

    private bool _levelsNormalized;

    /// <summary>
    /// Gets or sets the units at each location. <see cref="OnHand"/> and <see cref="Reserved"/> are their totals.
    /// </summary>
    public List<StockLocationLevel> Levels { get; set; } = [];

    /// <summary>
    /// Gets the units at a location, adding the location with none when it has none yet.
    /// </summary>
    public StockLocationLevel GetLevel(string locationId)
    {
        NormalizeLevels();

        locationId = string.IsNullOrEmpty(locationId) ? InventoryConstants.DefaultLocationId : locationId;

        var level = Levels.FirstOrDefault(entry => entry.LocationId == locationId);

        if (level is null)
        {
            level = new StockLocationLevel { LocationId = locationId };
            Levels.Add(level);
        }

        return level;
    }

    /// <summary>
    /// Gets the units at a location, or <see langword="null"/> when it has none.
    /// </summary>
    public StockLocationLevel FindLevel(string locationId)
    {
        NormalizeLevels();

        locationId = string.IsNullOrEmpty(locationId) ? InventoryConstants.DefaultLocationId : locationId;

        return Levels.FirstOrDefault(entry => entry.LocationId == locationId);
    }

    /// <summary>
    /// Makes the units at each location add up to the totals, once for the item as loaded: an item counted before there
    /// were locations has all its units at the default location, and so has any difference. Changes made to a
    /// location's units after that are summed into the totals by <see cref="SumLevels"/>.
    /// </summary>
    public void NormalizeLevels()
    {
        Levels ??= [];

        if (_levelsNormalized)
        {
            return;
        }

        _levelsNormalized = true;

        var onHand = Levels.Sum(level => level.OnHand);
        var reserved = Levels.Sum(level => level.Reserved);

        if (onHand == OnHand && reserved == Reserved)
        {
            return;
        }

        var fallback = Levels.FirstOrDefault(level => level.LocationId == InventoryConstants.DefaultLocationId);

        if (fallback is null)
        {
            fallback = new StockLocationLevel { LocationId = InventoryConstants.DefaultLocationId };
            Levels.Insert(0, fallback);
        }

        fallback.OnHand += OnHand - onHand;
        fallback.Reserved += Reserved - reserved;
    }

    /// <summary>
    /// Sets the totals to the sum of the locations' units, after a location's units changed.
    /// </summary>
    public void SumLevels()
    {
        Levels ??= [];
        _levelsNormalized = true;
        OnHand = Levels.Sum(level => level.OnHand);
        Reserved = Levels.Sum(level => level.Reserved);
    }
}

/// <summary>
/// The units of a stock item at one location.
/// </summary>
public sealed class StockLocationLevel
{
    public string LocationId { get; set; }

    public int OnHand { get; set; }

    public int Reserved { get; set; }

    [JsonIgnore]
    public int Available => OnHand - Reserved;
}
