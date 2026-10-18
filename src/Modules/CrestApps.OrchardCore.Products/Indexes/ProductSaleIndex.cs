using YesSql.Indexes;

namespace CrestApps.OrchardCore.Products.Indexes;

/// <summary>
/// When a product's sale starts and ends, so its search record is built again at those moments.
/// </summary>
public sealed class ProductSaleIndex : MapIndex
{
    public string ContentItemId { get; set; }

    public DateTime? StartsUtc { get; set; }

    public DateTime? EndsUtc { get; set; }
}
