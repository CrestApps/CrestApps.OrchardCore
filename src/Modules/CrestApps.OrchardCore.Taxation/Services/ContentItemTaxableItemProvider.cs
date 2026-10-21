using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Converts a content item that has a <see cref="TaxationPart"/> into an <see cref="ITaxableItem"/>.
/// The amount is the item's <see cref="ProductPart.Price"/> when it has one.
/// </summary>
/// <remarks>
/// When the item does not classify itself (its <see cref="TaxationPart.TaxCategoryCode"/> is empty), the
/// registered <see cref="ITaxClassificationProvider"/> instances are consulted in order so that the item
/// can inherit its classification, for example from the taxonomy terms (product categories) it belongs to.
/// An explicit item classification always overrides an inherited one.
/// </remarks>
public sealed class ContentItemTaxableItemProvider : ITaxableItemProvider
{
    private readonly ITaxClassificationProvider[] _classificationProviders;

    public ContentItemTaxableItemProvider(IEnumerable<ITaxClassificationProvider> classificationProviders)
    {
        _classificationProviders = classificationProviders
            .OrderBy(provider => provider.Order)
            .ToArray();
    }

    /// <inheritdoc />
    public int Order => 0;

    /// <inheritdoc />
    public bool CanCreate(object source)
        => source is ContentItem contentItem && contentItem.Has<TaxationPart>();

    /// <inheritdoc />
    public async ValueTask<ITaxableItem> CreateAsync(object source, CancellationToken cancellationToken = default)
    {
        if (source is not ContentItem contentItem)
        {
            return null;
        }

        var part = contentItem.Get<TaxationPart>(nameof(TaxationPart));

        if (part is null || !part.Taxable)
        {
            return null;
        }

        var categoryCode = part.TaxCategoryCode;
        var classificationCode = part.TaxClassificationCode;
        var externalTaxCode = part.ExternalTaxCode;

        // An explicit classification on the item wins. Otherwise inherit from the registered providers
        // (for example, the taxonomy terms the item belongs to).
        if (string.IsNullOrEmpty(categoryCode))
        {
            foreach (var provider in _classificationProviders)
            {
                var classification = await provider.GetClassificationAsync(contentItem, cancellationToken);

                if (classification is null || string.IsNullOrEmpty(classification.TaxCategoryCode))
                {
                    continue;
                }

                categoryCode = classification.TaxCategoryCode;

                if (string.IsNullOrEmpty(classificationCode))
                {
                    classificationCode = classification.TaxClassificationCode;
                }

                if (string.IsNullOrEmpty(externalTaxCode))
                {
                    externalTaxCode = classification.ExternalTaxCode;
                }

                break;
            }
        }

        var unitPrice = contentItem.TryGet<ProductPart>(out var product)
            ? product.Price ?? 0m
            : 0m;

        var item = new TaxableItem
        {
            Id = contentItem.ContentItemId,
            UnitPrice = unitPrice,
            Quantity = 1m,
            TaxCategoryCode = categoryCode,
            TaxClassificationCode = classificationCode,
            ExternalTaxCode = externalTaxCode,
        };

        item.Metadata["ContentType"] = contentItem.ContentType;

        return item;
    }
}
