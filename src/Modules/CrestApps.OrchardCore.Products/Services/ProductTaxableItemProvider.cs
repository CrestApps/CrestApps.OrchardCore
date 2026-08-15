using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Payments.Core.Models;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Converts a product content item (one that has a <see cref="ProductPart"/> and an opted-in
/// <c>TaxationPart</c>) into an <see cref="ITaxableItem"/>. Unlike the generic content-item provider,
/// this one maps the configured <see cref="ProductType"/> to the appropriate <see cref="TaxableItemKind"/>
/// and reads the price from the strongly typed part. It never calculates tax; it only exposes the
/// tax-relevant information so the taxation framework can determine the applicable tax.
/// </summary>
/// <remarks>
/// The <c>TaxationPart</c> is read via its JSON projection (rather than the strongly typed part) so the
/// Products module does not take a hard reference on the Taxation module, keeping taxation optional. This
/// mirrors how the Subscriptions module consumes the same part.
/// </remarks>
public sealed class ProductTaxableItemProvider : ITaxableItemProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ProductTaxableItemProvider(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
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

        var productPart = contentItem.Get<ProductPart>(nameof(ProductPart));

        if (productPart is null)
        {
            return null;
        }

        var item = new TaxableItem
        {
            Id = contentItem.ContentItemId,
            Kind = await ResolveKindAsync(contentItem),
            UnitPrice = (decimal)productPart.Price,
            Quantity = 1m,
            TaxCategoryCode = taxationPart["TaxCategoryCode"]?.GetValue<string>(),
            TaxClassificationCode = taxationPart["TaxClassificationCode"]?.GetValue<string>(),
            ExternalTaxCode = taxationPart["ExternalTaxCode"]?.GetValue<string>(),
        };

        item.Metadata["ContentType"] = contentItem.ContentType;

        return item;
    }

    private async ValueTask<TaxableItemKind> ResolveKindAsync(ContentItem contentItem)
    {
        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

        var partDefinition = definition?.Parts
            .FirstOrDefault(part => part.PartDefinition.Name == nameof(ProductPart));

        var type = partDefinition?.GetSettings<ProductPartSettings>().Type ?? ProductType.Undefined;

        return type switch
        {
            ProductType.Good => TaxableItemKind.Physical,
            ProductType.Service => TaxableItemKind.Service,
            ProductType.Digital => TaxableItemKind.Digital,
            _ => TaxableItemKind.Physical,
        };
    }
}
