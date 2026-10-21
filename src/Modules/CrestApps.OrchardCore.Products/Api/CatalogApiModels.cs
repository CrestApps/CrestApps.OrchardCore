using CrestApps.OrchardCore.Commerce.Api;

namespace CrestApps.OrchardCore.Products.Api;

/// <summary>
/// A product in a list: enough to show a card and link to the product.
/// </summary>
/// <param name="Id">The product's content item id.</param>
/// <param name="Type">The product's content type.</param>
/// <param name="Title">The product's name.</param>
/// <param name="Sku">The product's SKU, when it has one.</param>
/// <param name="Price">What the caller pays for one, discounts for their role included.</param>
/// <param name="CompareAtPrice">The price it's marked down from, when it's on sale.</param>
/// <param name="IsPurchasable">Whether it can be bought online, rather than only enquired about.</param>
/// <param name="ImageUrl">The product's first image.</param>
/// <param name="Url">The path of the product's page on the site.</param>
public sealed record ApiProductSummary(
    string Id,
    string Type,
    string Title,
    string Sku,
    ApiMoney Price,
    ApiMoney CompareAtPrice,
    bool IsPurchasable,
    string ImageUrl,
    string Url);

/// <summary>
/// A product with what it takes to sell it: its prices, options and variants, add-ons, specifications and stock.
/// </summary>
public sealed record ApiProduct(
    string Id,
    string Type,
    string Title,
    string Sku,
    ApiMoney Price,
    ApiMoney CompareAtPrice,
    bool IsPurchasable,
    string Url,
    IReadOnlyList<ApiProductImage> Images,
    IReadOnlyList<ApiProductPrice> Prices,
    IReadOnlyList<ApiProductOption> Options,
    IReadOnlyList<ApiProductVariant> Variants,
    IReadOnlyList<ApiProductAddOn> AddOns,
    IReadOnlyList<ApiProductAttribute> Attributes,
    ApiQuantityRules Quantity,
    ApiAvailability Availability);

/// <summary>
/// A product image, at the size a product page shows and as a thumbnail.
/// </summary>
public sealed record ApiProductImage(string Url, string ThumbnailUrl, string Alt);

/// <summary>
/// One of the prices a product lists, such as a one-time price and a monthly plan. A product that lists none is sold
/// at its <see cref="ApiProduct.Price"/>.
/// </summary>
/// <param name="Id">The price's id, to add the product to a cart at this price.</param>
/// <param name="Name">The price's name, such as "Monthly".</param>
/// <param name="Amount">What the price charges each time.</param>
/// <param name="Kind"><c>oneTime</c> or <c>recurring</c>.</param>
/// <param name="Interval">How often a recurring price charges: <c>day</c>, <c>week</c>, <c>month</c> or <c>year</c>.</param>
/// <param name="IntervalCount">How many intervals pass between charges.</param>
/// <param name="TrialDays">The free days before the first charge.</param>
/// <param name="SetupFee">What the first charge adds once.</param>
/// <param name="IsDefault">Whether it's the price chosen when none is.</param>
/// <param name="AllowCustomAmount">Whether the buyer names the amount, such as a donation, between the limits.</param>
/// <param name="MinimumAmount">The least a custom amount can be.</param>
/// <param name="MaximumAmount">The most a custom amount can be.</param>
public sealed record ApiProductPrice(
    string Id,
    string Name,
    ApiMoney Amount,
    string Kind,
    string Interval,
    int? IntervalCount,
    int? TrialDays,
    ApiMoney SetupFee,
    bool IsDefault,
    bool AllowCustomAmount,
    ApiMoney MinimumAmount,
    ApiMoney MaximumAmount);

/// <summary>
/// An option the product's variants differ by, such as Size, and its values.
/// </summary>
public sealed record ApiProductOption(string Name, IReadOnlyList<string> Values);

/// <summary>
/// One variant of a product: a combination of option values with its own SKU, price and stock.
/// </summary>
public sealed record ApiProductVariant(
    string Id,
    string Title,
    IReadOnlyDictionary<string, string> Options,
    string Sku,
    ApiMoney Price,
    ApiMoney CompareAtPrice,
    string ImageUrl,
    ApiAvailability Availability);

/// <summary>
/// Something the buyer can add to a product, such as an engraving: a text they type, or a box they tick.
/// </summary>
/// <param name="Id">The add-on's id, the key of its value when adding the product to a cart.</param>
/// <param name="Label">What the buyer is asked.</param>
/// <param name="Kind"><c>text</c> or <c>checkbox</c>.</param>
/// <param name="Price">What the add-on adds to each unit.</param>
/// <param name="Required">Whether the product can't be added without it.</param>
/// <param name="MaxLength">The longest a text add-on can be.</param>
public sealed record ApiProductAddOn(string Id, string Label, string Kind, ApiMoney Price, bool Required, int? MaxLength);

/// <summary>
/// A specification of the product, such as its material: the label and the value shown.
/// </summary>
public sealed record ApiProductAttribute(string Key, string Label, string Value);

/// <summary>
/// How many units can be bought at once: the least, the most, and the step between.
/// </summary>
public sealed record ApiQuantityRules(int Minimum, int? Maximum, int Step);

/// <summary>
/// Whether a product can be bought now.
/// </summary>
/// <param name="Status"><c>inStock</c>, <c>lowStock</c>, <c>outOfStock</c>, <c>backorder</c> or <c>preorder</c>.</param>
/// <param name="Available">How many are left, given only when few are, the way the product page says "Only 3 left".</param>
/// <param name="CanBuy">Whether one can be added to a cart now.</param>
/// <param name="ReleaseDateUtc">When a preordered product is released.</param>
public sealed record ApiAvailability(string Status, int? Available, bool CanBuy, DateTime? ReleaseDateUtc);
