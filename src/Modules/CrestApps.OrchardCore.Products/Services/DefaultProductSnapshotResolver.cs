using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// The default <see cref="IProductSnapshotResolver"/>. It reads the strongly typed <see cref="ProductPart"/>
/// for identity and price, the content type's <see cref="ProductPartSettings"/> for the product type, and
/// the optional <c>TaxationPart</c> JSON projection for tax classification. It never calculates tax or
/// price; it only projects the content item into a stable, provider-neutral snapshot.
/// </summary>
/// <remarks>
/// The <c>TaxationPart</c> is read via its JSON projection (rather than the strongly typed part) so the
/// Products module does not take a hard reference on the Taxation module, keeping taxation optional.
/// </remarks>
public sealed class DefaultProductSnapshotResolver : IProductSnapshotResolver
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultProductSnapshotResolver"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager used to read part settings.</param>
    public DefaultProductSnapshotResolver(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <inheritdoc/>
    public async Task<ISellableProduct> ResolveAsync(ProductSnapshotContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var contentItem = context.ContentItem;

        if (contentItem is null)
        {
            return null;
        }

        var productPart = contentItem.Get<ProductPart>(nameof(ProductPart));

        if (productPart is null)
        {
            return null;
        }

        JsonNode taxationPart = contentItem.Content?["TaxationPart"];

        return new SellableProduct
        {
            ContentItemId = contentItem.ContentItemId,
            ContentItemVersionId = contentItem.ContentItemVersionId,
            ContentType = contentItem.ContentType,
            Sku = productPart.Sku,
            Title = contentItem.DisplayText,
            UnitPrice = productPart.Price,
            Currency = await ResolveCurrencyAsync(contentItem, productPart),
            ProductType = await ResolveTypeAsync(contentItem),
            TaxCategoryCode = taxationPart?["TaxCategoryCode"]?.GetValue<string>(),
            TaxClassificationCode = taxationPart?["TaxClassificationCode"]?.GetValue<string>(),
            ExternalTaxCode = taxationPart?["ExternalTaxCode"]?.GetValue<string>(),
        };
    }

    private async ValueTask<string> ResolveCurrencyAsync(ContentItem contentItem, ProductPart productPart)
    {
        if (!string.IsNullOrEmpty(productPart.Currency))
        {
            return productPart.Currency.Trim().ToUpperInvariant();
        }

        var settings = await GetPartSettingsAsync(contentItem);
        var defaultCurrency = settings?.DefaultCurrency;

        return string.IsNullOrEmpty(defaultCurrency)
            ? null
            : defaultCurrency.Trim().ToUpperInvariant();
    }

    private async ValueTask<ProductType> ResolveTypeAsync(ContentItem contentItem)
    {
        var settings = await GetPartSettingsAsync(contentItem);

        return settings?.Type ?? ProductType.Undefined;
    }

    private async ValueTask<ProductPartSettings> GetPartSettingsAsync(ContentItem contentItem)
    {
        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

        var partDefinition = definition?.Parts
            .FirstOrDefault(part => part.PartDefinition.Name == nameof(ProductPart));

        return partDefinition?.GetSettings<ProductPartSettings>();
    }
}
