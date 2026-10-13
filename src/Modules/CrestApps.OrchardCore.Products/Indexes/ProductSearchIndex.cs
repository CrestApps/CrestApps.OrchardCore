using YesSql.Indexes;

namespace CrestApps.OrchardCore.Products.Indexes;

/// <summary>
/// One row per listed product: the columns a listing filters and sorts on directly.
/// </summary>
public sealed class ProductSearchIndex : MapIndex
{
    public string ContentItemId { get; set; }

    public string ContentType { get; set; }

    public string Culture { get; set; }

    public string Title { get; set; }

    public string Sku { get; set; }

    public decimal? Price { get; set; }

    public bool IsPurchasable { get; set; }

    public DateTime? PublishedUtc { get; set; }
}
