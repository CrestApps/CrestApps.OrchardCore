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
public sealed class ContentItemTaxableItemProvider : ITaxableItemProvider
{
    /// <inheritdoc />
    public int Order => 0;

    /// <inheritdoc />
    public bool CanCreate(object source)
        => source is ContentItem contentItem && contentItem.Has<TaxationPart>();

    /// <inheritdoc />
    public ValueTask<ITaxableItem> CreateAsync(object source, CancellationToken cancellationToken = default)
    {
        if (source is not ContentItem contentItem)
        {
            return ValueTask.FromResult<ITaxableItem>(null);
        }

        var part = contentItem.Get<TaxationPart>(nameof(TaxationPart));

        if (part is null || !part.Taxable)
        {
            return ValueTask.FromResult<ITaxableItem>(null);
        }

        var item = new TaxableItem
        {
            Id = contentItem.ContentItemId,
            UnitPrice = ReadPrice(contentItem),
            Quantity = 1m,
            TaxCategoryCode = part.TaxCategoryCode,
            TaxClassificationCode = part.TaxClassificationCode,
            ExternalTaxCode = part.ExternalTaxCode,
        };

        item.Metadata["ContentType"] = contentItem.ContentType;

        return ValueTask.FromResult<ITaxableItem>(item);
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
