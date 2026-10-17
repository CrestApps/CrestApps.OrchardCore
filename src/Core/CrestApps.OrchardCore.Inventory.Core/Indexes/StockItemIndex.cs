using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Inventory.Core.Indexes;

public sealed class StockItemIndex : CatalogItemIndex
{
    public string ContentItemId { get; set; }

    public string VariantId { get; set; }

    public string LocationId { get; set; }

    public string NormalizedSku { get; set; }

    public string NormalizedTitle { get; set; }

    public int OnHand { get; set; }

    public int Available { get; set; }

    public bool IsLow { get; set; }

    public bool AllowBackorder { get; set; }

    public bool Archived { get; set; }

    public DateTime ModifiedUtc { get; set; }
}

public sealed class StockItemIndexProvider : IndexProvider<StockItem>
{
    public StockItemIndexProvider()
    {
        CollectionName = InventoryConstants.CollectionName;
    }

    public override void Describe(DescribeContext<StockItem> context)
    {
        context.For<StockItemIndex>()
            .Map(item => new StockItemIndex
            {
                ItemId = item.ItemId,
                ContentItemId = item.ContentItemId,
                VariantId = item.VariantId,
                LocationId = item.LocationId,
                NormalizedSku = Normalize(item.Sku),
                NormalizedTitle = Normalize(item.Title),
                OnHand = item.OnHand,
                Available = item.Available,
                IsLow = item.IsLow,
                AllowBackorder = item.AllowBackorder,
                Archived = item.Archived,
                ModifiedUtc = item.ModifiedUtc,
            });
    }

    internal static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToUpperInvariant();

        return normalized.Length > 255 ? normalized[..255] : normalized;
    }
}
