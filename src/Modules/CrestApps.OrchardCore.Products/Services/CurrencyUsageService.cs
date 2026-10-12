using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Finds the products priced in a currency. A currency still in use can't be deleted: its products would keep a
/// code the editors no longer list, and saving one of them would ask for a currency that no longer exists.
/// </summary>
public sealed class CurrencyUsageService
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyUsageService"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager, used to find the product types.</param>
    /// <param name="session">The YesSql session the products are read from.</param>
    public CurrencyUsageService(IContentDefinitionManager contentDefinitionManager, ISession session)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _session = session;
    }

    /// <summary>
    /// Returns the titles of the products, published or draft, that are priced in the currency.
    /// </summary>
    /// <param name="currencyCode">The ISO-4217 currency code.</param>
    public async Task<IReadOnlyList<string>> GetProductsUsingAsync(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return [];
        }

        var productTypes = (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(type => type.Parts.Any(part => part.PartDefinition.Name is nameof(ProductPart) or nameof(ProductPricePart)))
            .Select(type => type.Name)
            .ToArray();

        if (productTypes.Length == 0)
        {
            return [];
        }

        var items = await _session
            .Query<ContentItem, ContentItemIndex>(index => index.ContentType.IsIn(productTypes) && (index.Latest || index.Published))
            .ListAsync();

        return items
            .Where(item => UsesCurrency(item, currencyCode))
            .Select(item => string.IsNullOrEmpty(item.DisplayText) ? item.ContentItemId : item.DisplayText)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool UsesCurrency(ContentItem item, string currencyCode)
    {
        if (item.TryGet<ProductPart>(out var product) &&
            string.Equals(product.Currency, currencyCode, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return item.TryGet<ProductPricePart>(out var pricing) &&
            pricing.Prices is not null &&
            pricing.Prices.Any(price => string.Equals(price.Currency, currencyCode, StringComparison.OrdinalIgnoreCase));
    }
}
