using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// Tells the product listing which product types carry a position, the order their merchant arranged them in,
/// so a listing of those types offers a "Recommended" sort. The module that knows the position also adds it to
/// each product's search record, as a <see cref="ProductAttributeKeys.Position"/> value, through an
/// <see cref="IProductSearchDocumentContributor"/>.
/// </summary>
public interface IProductPositionProvider
{
    /// <summary>
    /// Whether products of the type carry a position.
    /// </summary>
    /// <param name="definition">The product type.</param>
    bool HasPositions(ContentTypeDefinition definition);
}
