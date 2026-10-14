namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// The shape of product images on a listing page.
/// </summary>
public enum ProductImageAspectRatio
{
    /// <summary>
    /// Four wide by three high, which suits vehicles and property.
    /// </summary>
    Landscape = 0,

    /// <summary>
    /// As wide as it is high, which suits most retail products.
    /// </summary>
    Square = 1,

    /// <summary>
    /// Three wide by four high, which suits clothing on a model.
    /// </summary>
    Portrait = 2,

    /// <summary>
    /// Sixteen wide by nine high.
    /// </summary>
    Wide = 3,
}
