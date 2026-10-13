namespace CrestApps.OrchardCore.Products.Core.Models;

/// <summary>
/// Whether the products of a content type can be bought online or are only listed.
/// </summary>
/// <remarks>
/// Listing and selling are separate decisions. A house for sale or a car on the lot is shown, filtered and
/// opened like any product, but nobody checks it out. The decision is made once per content type, and
/// <see cref="PerItem"/> hands it to each item, for a dealer that sells some cars online and only lists others.
/// </remarks>
public enum ProductSalesMode
{
    /// <summary>
    /// Every product of the type can be bought online. This is the default, so existing product types keep selling.
    /// </summary>
    Purchasable = 0,

    /// <summary>
    /// Products of the type are listed but can never be bought online.
    /// </summary>
    ListingOnly = 1,

    /// <summary>
    /// Each product decides with its own <see cref="ProductPart.IsPurchasable"/> flag.
    /// </summary>
    PerItem = 2,
}
