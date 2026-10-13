using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Products.Core.Models;

namespace CrestApps.OrchardCore.Carts.ViewModels;

/// <summary>
/// The Add to cart action on a product's card and page.
/// </summary>
public class AddToCartViewModel
{
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the one-time prices the product can be bought at now. It is empty for a product priced on its
    /// product part, which is bought at that price.
    /// </summary>
    public IList<ProductPrice> Prices { get; set; } = [];

    public string DefaultPriceId { get; set; }

    /// <summary>
    /// Gets or sets the most units that can be added at once, 1 when only one can be bought.
    /// </summary>
    public int MaxQuantity { get; set; }

    /// <summary>
    /// Gets or sets where the visitor returns to after adding: the page they added from.
    /// </summary>
    public string ReturnUrl { get; set; }
}

/// <summary>
/// The cart page.
/// </summary>
public class CartViewModel
{
    public CartPricing Pricing { get; set; }

    /// <summary>
    /// Gets or sets where the cart is checked out, or <see langword="null"/> when no module checks carts out
    /// (the Orders feature is off).
    /// </summary>
    public string CheckoutUrl { get; set; }
}

/// <summary>
/// The cart link in the site header.
/// </summary>
public class CartLinkViewModel
{
    public int ItemCount { get; set; }

    public string Url { get; set; }
}
