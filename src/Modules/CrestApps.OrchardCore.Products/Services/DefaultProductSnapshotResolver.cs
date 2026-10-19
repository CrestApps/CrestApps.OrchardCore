using OrchardCore.Modules;
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
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultProductSnapshotResolver"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager used to read part settings.</param>
    public DefaultProductSnapshotResolver(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public DefaultProductSnapshotResolver(IContentDefinitionManager contentDefinitionManager, IClock clock)
        : this(contentDefinitionManager)
    {
        _clock = clock;
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

        var settings = await GetPartSettingsAsync(contentItem);

        // A listed product is shown but never sold. Every checkout path resolves its snapshot here, so refusing
        // it here is what stops a crafted request from buying something the merchant only lists.
        if (!productPart.CanBePurchased(settings))
        {
            return null;
        }

        // A product with variants is sold as one of them, and the variant decides the SKU and the price. One the
        // merchant switched off, or one that no longer exists, is not for sale.
        ProductVariant variant = null;
        string variantTitle = null;

        if (contentItem.TryGet<ProductVariantsPart>(out var variants) && variants.HasVariants)
        {
            variant = variants.Find(context.VariantId);

            if (variant is null || !variant.Enabled)
            {
                return null;
            }

            variantTitle = variant.GetTitle(variants.Options?.Select(option => option.Name));
        }

        // During a sale the product sells at its sale price.
        var unitPrice = ProductSale.GetPrice(productPart, variant, _clock?.UtcNow ?? DateTime.UtcNow);

        // A product with no price of its own can still be sold at the prices it lists; with neither, there is
        // nothing to charge.
        if (!unitPrice.HasValue &&
            !(contentItem.TryGet<ProductPricePart>(out var pricing) && pricing.Prices is { Count: > 0 }))
        {
            return null;
        }

        JsonNode taxationPart = contentItem.Content?["TaxationPart"];

        return new SellableProduct
        {
            ContentItemId = contentItem.ContentItemId,
            ContentItemVersionId = contentItem.ContentItemVersionId,
            ContentType = contentItem.ContentType,
            Sku = string.IsNullOrEmpty(variant?.Sku) ? productPart.Sku : variant.Sku,
            Title = string.IsNullOrEmpty(variantTitle) ? contentItem.DisplayText : $"{contentItem.DisplayText} ({variantTitle})",
            UnitPrice = unitPrice ?? 0m,
            Currency = ResolveCurrency(productPart, settings),
            ProductType = settings?.Type ?? ProductType.Undefined,
            TaxCategoryCode = taxationPart?["TaxCategoryCode"]?.GetValue<string>(),
            TaxClassificationCode = taxationPart?["TaxClassificationCode"]?.GetValue<string>(),
            ExternalTaxCode = taxationPart?["ExternalTaxCode"]?.GetValue<string>(),
            VariantId = variant?.VariantId,
            VariantTitle = variantTitle,
        };
    }

    private static string ResolveCurrency(ProductPart productPart, ProductPartSettings settings)
    {
        if (!string.IsNullOrEmpty(productPart.Currency))
        {
            return productPart.Currency.Trim().ToUpperInvariant();
        }

        var defaultCurrency = settings?.DefaultCurrency;

        return string.IsNullOrEmpty(defaultCurrency)
            ? null
            : defaultCurrency.Trim().ToUpperInvariant();
    }

    private async ValueTask<ProductPartSettings> GetPartSettingsAsync(ContentItem contentItem)
    {
        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

        var partDefinition = definition?.Parts
            .FirstOrDefault(part => part.PartDefinition.Name == nameof(ProductPart));

        return partDefinition?.GetSettings<ProductPartSettings>();
    }
}
