using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Payments.Core.Models;
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
            UnitPrice = (decimal)productPart.Price,
            Currency = context.Currency,
            ProductType = await ResolveTypeAsync(contentItem),
            TaxCategoryCode = taxationPart?["TaxCategoryCode"]?.GetValue<string>(),
            TaxClassificationCode = taxationPart?["TaxClassificationCode"]?.GetValue<string>(),
            ExternalTaxCode = taxationPart?["ExternalTaxCode"]?.GetValue<string>(),
        };
    }

    private async ValueTask<ProductType> ResolveTypeAsync(ContentItem contentItem)
    {
        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

        var partDefinition = definition?.Parts
            .FirstOrDefault(part => part.PartDefinition.Name == nameof(ProductPart));

        return partDefinition?.GetSettings<ProductPartSettings>().Type ?? ProductType.Undefined;
    }
}
