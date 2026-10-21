using YesSql.Indexes;

namespace CrestApps.OrchardCore.Inventory.Core.Models;

/// <summary>
/// A shopper's request to be emailed when a product, or one of its variants, is back in stock. It's sent once.
/// </summary>
public sealed class StockAlert
{
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the stock item asked about.
    /// </summary>
    public string StockItemId { get; set; }

    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the variant asked about, or <see langword="null"/> for a product without variants.
    /// </summary>
    public string VariantId { get; set; }

    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the signed-in user who asked, or <see langword="null"/> for a guest.
    /// </summary>
    public string UserId { get; set; }

    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the email went out, or <see langword="null"/> while it's waiting.
    /// </summary>
    public DateTime? SentUtc { get; set; }
}

public sealed class StockAlertIndex : MapIndex
{
    public string StockItemId { get; set; }

    public string ContentItemId { get; set; }

    public string VariantId { get; set; }

    public string NormalizedEmail { get; set; }

    public string UserId { get; set; }

    public bool Waiting { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public sealed class StockAlertIndexProvider : IndexProvider<StockAlert>
{
    public StockAlertIndexProvider()
    {
        CollectionName = InventoryConstants.CollectionName;
    }

    public override void Describe(DescribeContext<StockAlert> context)
        => context.For<StockAlertIndex>().Map(alert => new StockAlertIndex
        {
            StockItemId = alert.StockItemId,
            ContentItemId = alert.ContentItemId,
            VariantId = alert.VariantId,
            NormalizedEmail = alert.Email?.Trim().ToUpperInvariant(),
            UserId = alert.UserId,
            Waiting = alert.SentUtc is null,
            CreatedUtc = alert.CreatedUtc,
        });
}
