using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Inventory.Services;

/// <summary>
/// Says in a product's search record whether it is in stock: some of it, or of one of its variants, is available.
/// A product that isn't counted can always be bought, so it is in stock. A product on backorder isn't: it can be
/// ordered, but not shipped now.
/// </summary>
internal sealed class InventorySearchDocumentContributor : IProductSearchDocumentContributor
{
    private readonly IEnumerable<IProductAvailabilityProvider> _availabilityProviders;

    public InventorySearchDocumentContributor(IEnumerable<IProductAvailabilityProvider> availabilityProviders)
    {
        _availabilityProviders = availabilityProviders;
    }

    public async Task ContributeAsync(ContentItem contentItem, ProductSearchDocument document)
    {
        var availability = await _availabilityProviders.GetAvailabilityAsync([new ProductStockKey(contentItem.ContentItemId, null)]);
        var inStock = availability.Count == 0 || availability.Values.Any(value => value.Available > 0);

        document.Attributes.Add(new ProductAttributeValue
        {
            Key = ProductAttributeKeys.InStock,
            Text = inStock ? "true" : "false",
        });
    }
}
