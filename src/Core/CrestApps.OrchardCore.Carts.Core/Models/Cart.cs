using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Carts.Core.Models;

/// <summary>
/// A visitor's shopping cart: the products they mean to buy, in one currency.
/// </summary>
/// <remarks>
/// A cart belongs to a signed-in user, or to a guest identified by a cookie whose token is never stored, only
/// its hash. It holds what was chosen, not what it costs: every line is priced again whenever the cart is
/// shown and when it becomes an order, so a total from the browser is never trusted.
/// </remarks>
public sealed class Cart : CatalogItem
{
    /// <summary>
    /// Gets or sets the YesSql document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the user the cart belongs to, or <see langword="null"/> for a guest's cart.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the hash of the guest's cart token, or <see langword="null"/> for a user's cart.
    /// </summary>
    public string GuestTokenHash { get; set; }

    /// <summary>
    /// Gets or sets the currency of every line. It is set by the first product added, because prices are not
    /// converted.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the lines, in the order they were added.
    /// </summary>
    public List<CartLine> Lines { get; set; } = [];

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the cart is deleted if nobody touches it again.
    /// </summary>
    public DateTime ExpiresUtc { get; set; }

    /// <summary>
    /// Gets the number of items in the cart, counting each unit.
    /// </summary>
    public int ItemCount
        => Lines?.Sum(line => line.Quantity) ?? 0;
}

/// <summary>
/// One product in a cart, at one of its prices.
/// </summary>
public sealed class CartLine
{
    /// <summary>
    /// Gets or sets the add-ons the buyer chose, by add-on id: what they typed, or <c>true</c> for a ticked box. The
    /// same product with other add-ons is another line.
    /// </summary>
    public Dictionary<string, string> AddOns { get; set; }

    /// <summary>
    /// Gets or sets the line's identifier, used to change or remove it.
    /// </summary>
    public string LineId { get; set; }

    /// <summary>
    /// Gets or sets the product.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the product's price the line is bought at, or <see langword="null"/> for a product priced on
    /// its product part.
    /// </summary>
    public string PriceId { get; set; }

    /// <summary>
    /// Gets or sets the variant bought, for a product sold in variants.
    /// </summary>
    public string VariantId { get; set; }

    /// <summary>
    /// Gets or sets how many are wanted.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the unit price when the line was added, so a price that changed since can be pointed out.
    /// It is never charged.
    /// </summary>
    public decimal AddedUnitPrice { get; set; }

    public DateTime AddedUtc { get; set; }
}
