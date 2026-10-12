using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Converts a product content item (one that has a <see cref="ProductPart"/> and an opted-in
/// <c>TaxationPart</c>) into an <see cref="ITaxableItem"/>. Unlike the generic content-item provider,
/// this one resolves the product through the sellable snapshot seam, so the taxable item carries the
/// product-owned currency, unit price, and product-type–derived <see cref="TaxableItemKind"/>. It never
/// calculates tax; it only exposes the tax-relevant information so the taxation framework can determine the
/// applicable tax.
/// </summary>
/// <remarks>
/// The <c>TaxationPart</c> is read via its JSON projection (rather than the strongly typed part) so the
/// Products module does not take a hard reference on the Taxation module, keeping taxation optional. This
/// mirrors how the Subscriptions module consumes the same part.
/// </remarks>
public sealed class ProductTaxableItemProvider : ITaxableItemProvider
{
    private readonly IProductSnapshotResolver _snapshotResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductTaxableItemProvider"/> class.
    /// </summary>
    /// <param name="snapshotResolver">The resolver that projects a product content item into a sellable snapshot.</param>
    public ProductTaxableItemProvider(IProductSnapshotResolver snapshotResolver)
    {
        _snapshotResolver = snapshotResolver;
    }

    // Runs before the generic content-item provider so products get the richer, product-aware mapping.
    public int Order => -10;

    public bool CanCreate(object source)
        => source is ContentItem contentItem &&
            contentItem.Has<ProductPart>() &&
            contentItem.Content?["TaxationPart"] is not null;

    public async ValueTask<ITaxableItem> CreateAsync(object source, CancellationToken cancellationToken = default)
    {
        if (source is not ContentItem contentItem)
        {
            return null;
        }

        JsonNode taxationPart = contentItem.Content?["TaxationPart"];

        if (taxationPart is null)
        {
            return null;
        }

        // Default to taxable when the flag is absent to match the part's own default.
        var taxable = taxationPart["Taxable"]?.GetValue<bool>() ?? true;

        if (!taxable)
        {
            return null;
        }

        var snapshot = await _snapshotResolver.ResolveAsync(new ProductSnapshotContext(contentItem), cancellationToken);

        if (snapshot is null)
        {
            return null;
        }

        // A product owns the currency it is sold in. A product with no currency (neither its own nor its
        // content type's default) is not sellable, so it must not be silently taxed in the calculation
        // context currency; fail closed rather than fall through to the currency-agnostic generic provider.
        if (string.IsNullOrEmpty(snapshot.Currency))
        {
            throw new InvalidOperationException($"Product '{contentItem.ContentItemId}' declares no currency (set its Currency or the content type's Default currency); it cannot be taxed without one.");
        }

        var item = new TaxableItem
        {
            Id = contentItem.ContentItemId,
            Kind = MapKind(snapshot.ProductType),
            UnitPrice = snapshot.UnitPrice,
            Currency = snapshot.Currency,
            Quantity = 1m,
            TaxCategoryCode = taxationPart["TaxCategoryCode"]?.GetValue<string>(),
            TaxClassificationCode = taxationPart["TaxClassificationCode"]?.GetValue<string>(),
            ExternalTaxCode = taxationPart["ExternalTaxCode"]?.GetValue<string>(),
        };

        item.Metadata["ContentType"] = contentItem.ContentType;

        return item;
    }

    private static TaxableItemKind MapKind(ProductType type)
        => type switch
        {
            ProductType.Good => TaxableItemKind.Physical,
            ProductType.Service => TaxableItemKind.Service,
            ProductType.Digital => TaxableItemKind.Digital,
            _ => TaxableItemKind.Physical,
        };
}
