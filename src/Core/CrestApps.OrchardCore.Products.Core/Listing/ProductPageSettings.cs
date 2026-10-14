namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// Content-type-level settings for how a product's own page shows it.
/// </summary>
/// <remarks>
/// They are off by default, because product types that existed before listing (subscription plans) already
/// render their own price.
/// </remarks>
public sealed class ProductPageSettings
{
    /// <summary>
    /// Gets or sets whether the product page shows the price, the specification table and the product actions.
    /// </summary>
    public bool ShowDetails { get; set; }

    /// <summary>
    /// Gets or sets the label of the button shown on a product that cannot be bought online, such as
    /// "Request information". No button is shown when it is empty.
    /// </summary>
    public string InquiryLabel { get; set; }

    /// <summary>
    /// Gets or sets where the inquiry button leads, such as a contact page. <c>{id}</c> and <c>{title}</c>
    /// are replaced with the product's identifier and title.
    /// </summary>
    public string InquiryUrl { get; set; }
}
