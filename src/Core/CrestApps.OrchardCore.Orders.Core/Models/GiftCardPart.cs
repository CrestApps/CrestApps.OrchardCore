using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Orders.Core.Models;

/// <summary>
/// Makes a product a gift card: each one bought issues a gift card worth what was paid for it, emailed to the buyer.
/// </summary>
public sealed class GiftCardPart : ContentPart
{
}
