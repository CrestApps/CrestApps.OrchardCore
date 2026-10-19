using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Orders.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Orders.Core.Indexes;

/// <summary>
/// The queryable projection of a <see cref="GiftCard"/>.
/// </summary>
public sealed class GiftCardIndex : CatalogItemIndex
{
    /// <summary>
    /// The code in capitals without dashes (<see cref="GiftCardCodes.Normalize"/>).
    /// </summary>
    public string NormalizedCode { get; set; }

    /// <summary>
    /// The code's last four characters, which staff search by.
    /// </summary>
    public string LastCharacters { get; set; }

    public string RecipientEmail { get; set; }

    public decimal Balance { get; set; }

    public string Currency { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime? ExpiresUtc { get; set; }

    public string OrderId { get; set; }

    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// Maps <see cref="GiftCard"/> documents to <see cref="GiftCardIndex"/> rows.
/// </summary>
public sealed class GiftCardIndexProvider : IndexProvider<GiftCard>
{
    public GiftCardIndexProvider()
    {
        CollectionName = OrderConstants.CollectionName;
    }

    public override void Describe(DescribeContext<GiftCard> context)
    {
        context.For<GiftCardIndex>()
            .Map(card => new GiftCardIndex
            {
                ItemId = card.ItemId,
                NormalizedCode = GiftCardCodes.Normalize(card.Code),
                LastCharacters = card.LastCharacters,
                RecipientEmail = card.RecipientEmail?.Trim().ToLowerInvariant(),
                Balance = card.Balance,
                Currency = card.Currency,
                IsEnabled = card.IsEnabled,
                ExpiresUtc = card.ExpiresUtc,
                OrderId = card.OrderId,
                CreatedUtc = card.CreatedUtc,
            });
    }
}
