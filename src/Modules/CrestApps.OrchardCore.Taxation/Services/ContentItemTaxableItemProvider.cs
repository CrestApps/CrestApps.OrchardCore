using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Converts a content item that has a <see cref="TaxationPart"/> into an <see cref="ITaxableItem"/>.
/// The amount is read generically from a well-known <c>ProductPart.Price</c> location when present so
/// that the taxation module stays decoupled from any particular commerce module.
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

        var item = new TaxableItem
        {
            Id = contentItem.ContentItemId,
            UnitPrice = ReadPrice(contentItem),
            Quantity = 1m,
            TaxCategoryCode = categoryCode,
            TaxClassificationCode = classificationCode,
            ExternalTaxCode = externalTaxCode,
        };

        item.Metadata["ContentType"] = contentItem.ContentType;

        return item;
    }

    private static decimal ReadPrice(ContentItem contentItem)
    {
        JsonNode content = contentItem.Content;
        var priceNode = content?["ProductPart"]?["Price"];

        if (priceNode is JsonValue priceValue && priceValue.TryGetValue<decimal>(out var price))
        {
            return price;
        }

        return 0m;
    }
}
