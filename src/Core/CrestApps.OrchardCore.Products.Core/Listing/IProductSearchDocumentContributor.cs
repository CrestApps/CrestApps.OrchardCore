using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// Adds to the search record of a product, so a module that extends products can make them listable on its
/// terms without the listing knowing about it. Subscriptions, for example, fills in the billing period of a plan
/// whose price is set on its subscription part.
/// </summary>
/// <remarks>
/// Contributors run after the record is built from the product part, the product prices part and the product
/// attribute fields, in registration order. They are resolved when the record is built, never by a content
/// handler, so they may depend on <see cref="IContentManager"/>.
/// </remarks>
public interface IProductSearchDocumentContributor
{
    /// <summary>
    /// Adds to the search record of a product.
    /// </summary>
    /// <param name="contentItem">The published product.</param>
    /// <param name="document">The record being built.</param>
    Task ContributeAsync(ContentItem contentItem, ProductSearchDocument document);
}
