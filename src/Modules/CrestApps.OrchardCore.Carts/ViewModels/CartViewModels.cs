using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
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
    /// Gets or sets the variants a buyer can choose, for a product sold in variants.
    /// </summary>
    public IList<AddToCartVariantViewModel> Variants { get; set; } = [];

    /// <summary>
    /// Gets or sets the product's options and the values its variants on sale come in, in the product's order.
    /// </summary>
    public IList<AddToCartOptionViewModel> Options { get; set; } = [];

    /// <summary>
    /// Gets or sets the most units that can be added at once, 1 when only one can be bought.
    /// </summary>
    public int MaxQuantity { get; set; }

    /// <summary>
    /// Gets or sets the quantity the box starts at: the product's minimum, in packs.
    /// </summary>
    public int MinQuantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the pack size the quantity steps by.
    /// </summary>
    public int QuantityStep { get; set; } = 1;

    /// <summary>
    /// Gets or sets the product's volume discounts, smallest quantity first.
    /// </summary>
    public IList<CrestApps.OrchardCore.Products.Core.Models.ProductPriceTier> PriceTiers { get; set; } = [];

    /// <summary>
    /// Gets or sets the discount the signed-in buyer's role gets, when any.
    /// </summary>
    public CrestApps.OrchardCore.Products.Core.Models.ProductRoleDiscount RoleDiscount { get; set; }

    /// <summary>
    /// Gets or sets the personal touches the buyer can choose, such as an engraving. They are chosen on the product's
    /// page; a card offers them only through it.
    /// </summary>
    public IList<CrestApps.OrchardCore.Products.Core.Models.ProductAddOn> AddOns { get; set; } = [];

    /// <summary>
    /// Gets or sets the currency the add-on prices are shown in.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets whether the product needs something chosen on its own page first, such as a booking's date.
    /// </summary>
    public bool ChooseOnProductPage { get; set; }

    /// <summary>
    /// Gets or sets whether this is the product's own page, where add-ons are chosen.
    /// </summary>
    public bool IsProductPage { get; set; }

    /// <summary>
    /// Gets or sets the product's page, which a card with a required add-on links to.
    /// </summary>
    public string ProductUrl { get; set; }

    /// <summary>
    /// Gets or sets where the visitor returns to after adding: the page they added from.
    /// </summary>
    public string ReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets what's in stock of a product without variants, or <see langword="null"/> when its stock isn't
    /// counted. Variants carry their own.
    /// </summary>
    public AddToCartAvailabilityViewModel Availability { get; set; }
}

/// <summary>
/// The products a grouped product shows on its page, each with a quantity to buy, or on a card a link to that page.
/// </summary>
public class GroupedProductsViewModel
{
    public string ContentItemId { get; set; }

    public IList<GroupedProductRowViewModel> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets whether this is the grouped product's own page. A card links to it instead.
    /// </summary>
    public bool IsProductPage { get; set; }

    public string ProductUrl { get; set; }

    public string ReturnUrl { get; set; }
}

/// <summary>
/// A product in a group: its price, stock, and whether it's bought here or on its own page (one with variants or
/// options to choose).
/// </summary>
public class GroupedProductRowViewModel
{
    public string ContentItemId { get; set; }

    public string Title { get; set; }

    public string Url { get; set; }

    public string Price { get; set; }

    public AddToCartAvailabilityViewModel Availability { get; set; }

    /// <summary>
    /// Gets or sets whether the product has a variant, price or add-on to choose, which its own page offers.
    /// </summary>
    public bool ChooseOnItsPage { get; set; }

    public int MaxQuantity { get; set; }

    /// <summary>
    /// Gets or sets where an external product is sold, which the row links to instead of offering a quantity.
    /// </summary>
    public string ExternalUrl { get; set; }

    public string ExternalButtonText { get; set; }
}

/// <summary>
/// What a buyer is told about a product's stock, and whether it can be added.
/// </summary>
public class AddToCartAvailabilityViewModel
{
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets a CSS hint for the label: success, warning, danger or secondary.
    /// </summary>
    public string Tone { get; set; }

    public bool CanBuy { get; set; }

    /// <summary>
    /// Gets or sets whether the product is ordered ahead of its release, so the button says Pre-order.
    /// </summary>
    public bool IsPreorder { get; set; }
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

    /// <summary>
    /// Gets or sets what else can be done with the cart, such as asking for a quote.
    /// </summary>
    public IReadOnlyList<CartAction> Actions { get; set; } = [];
}

/// <summary>
/// The cart link in the site header.
/// </summary>
/// <summary>
/// Shown instead of the add to cart form when the shopper can't buy the product, such as outside its markets.
/// </summary>
public class AddToCartUnavailableViewModel
{
    public string Reason { get; set; }
}

public class CartLinkViewModel
{
    public int ItemCount { get; set; }

    public string Url { get; set; }
}

public class AddToCartVariantViewModel
{
    /// <summary>
    /// Gets or sets what's in stock of this variant, or <see langword="null"/> when its stock isn't counted.
    /// </summary>
    public AddToCartAvailabilityViewModel Availability { get; set; }

    public string VariantId { get; set; }

    /// <summary>
    /// Gets or sets the variant's values, such as "Red / M".
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the variant's price, formatted, or <see langword="null"/> when it sells at the product's price.
    /// </summary>
    public string Price { get; set; }

    public string Sku { get; set; }

    /// <summary>
    /// Gets or sets the variant's value for each option, by option name.
    /// </summary>
    public IDictionary<string, string> Options { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the media library path of the variant's image, one of the product's own images.
    /// </summary>
    public string ImagePath { get; set; }
}

public class AddToCartOptionViewModel
{
    public string Name { get; set; }

    public IList<string> Values { get; set; } = [];
}
