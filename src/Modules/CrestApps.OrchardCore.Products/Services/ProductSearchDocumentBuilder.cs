using CrestApps.OrchardCore.Products.Core.Services;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Services.Attributes;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Builds the search record of a published product from its content item.
/// </summary>
public sealed class ProductSearchDocumentBuilder
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ProductAttributeCatalog _attributeCatalog;
    private readonly IEnumerable<IProductSearchDocumentContributor> _contributors;
    private readonly IClock _clock;

    public ProductSearchDocumentBuilder(
        IContentDefinitionManager contentDefinitionManager,
        ProductAttributeCatalog attributeCatalog,
        IEnumerable<IProductSearchDocumentContributor> contributors,
        IClock clock)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _attributeCatalog = attributeCatalog;
        _contributors = contributors;
        _clock = clock;
    }

    /// <summary>
    /// Builds the record, or returns <see langword="null"/> when the item is not a product.
    /// </summary>
    /// <param name="contentItem">The published content item.</param>
    public async Task<ProductSearchDocument> BuildAsync(ContentItem contentItem)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

        if (!ProductAttributeCatalog.IsProductType(definition) ||
            !contentItem.TryGet<ProductPart>(out var product))
        {
            return null;
        }

        var settings = ProductAttributeCatalog.GetProductSettings(definition);
        var priceDisplay = product.GetPriceDisplay(settings);
        var isPurchasable = product.CanBePurchased(settings);

        // A product sold several ways is listed at the price a buyer gets without choosing: its default offered
        // price. Otherwise it is listed at the single price on its product part.
        var now = _clock.UtcNow;
        var offered = contentItem.TryGet<ProductPricePart>(out var pricing) ? pricing.GetDefault(now) : null;
        var price = offered?.Amount ?? ProductSale.GetPrice(product, null, now);
        var currency = !string.IsNullOrEmpty(offered?.Currency)
            ? offered.Currency
            : string.IsNullOrEmpty(product.Currency) ? settings?.DefaultCurrency : product.Currency;

        // A price that is not shown must not reach the index either: a price filter or a price sort over it
        // would tell visitors what the merchant chose not to.
        var shownPrice = priceDisplay == ProductPriceDisplay.Show ? price : null;

        var document = new ProductSearchDocument
        {
            ContentItemId = contentItem.ContentItemId,
            ContentItemVersionId = contentItem.ContentItemVersionId,
            ContentType = contentItem.ContentType,
            Culture = GetCulture(contentItem),
            Title = contentItem.DisplayText,
            Sku = product.Sku,
            Price = shownPrice,
            CompareAtPrice = shownPrice.HasValue && offered is null ? ProductSale.GetCompareAtPrice(product, null, now) : null,
            Currency = currency,
            BillingInterval = offered?.Kind == PriceKind.Recurring ? offered.Interval : null,
            BillingDuration = offered?.Kind == PriceKind.Recurring ? Math.Max(1, offered.BillingDuration ?? 1) : null,
            PriceDisplay = priceDisplay,
            IsPurchasable = isPurchasable,
            SaleStartsUtc = ProductSale.HasSalePrice(product, contentItem.TryGet<ProductVariantsPart>(out var saleVariants) ? saleVariants.Variants : null) ? product.SaleStartsUtc : null,
            SaleEndsUtc = ProductSale.HasSalePrice(product, saleVariants?.Variants) ? product.SaleEndsUtc : null,
            Featured = product.Featured,
            Position = product.Position,
            PublishedUtc = contentItem.PublishedUtc,
            CreatedUtc = contentItem.CreatedUtc,
        };

        document.Attributes.Add(new ProductAttributeValue
        {
            Key = ProductAttributeKeys.ContentType,
            Text = definition.Name,
            Label = definition.DisplayName,
        });

        document.Attributes.Add(new ProductAttributeValue
        {
            Key = ProductAttributeKeys.Purchasable,
            Text = isPurchasable ? "true" : "false",
        });

        document.Attributes.Add(new ProductAttributeValue
        {
            Key = ProductAttributeKeys.Featured,
            Text = product.Featured ? "true" : "false",
        });

        if (shownPrice.HasValue)
        {
            document.Attributes.Add(new ProductAttributeValue
            {
                Key = ProductAttributeKeys.Price,
                Text = FieldValueReader.ToInvariant(shownPrice.Value),
                Number = shownPrice.Value,
            });
        }

        var content = (JsonObject)contentItem.Content;

        foreach (var part in definition.Parts)
        {
            foreach (var field in part.PartDefinition.Fields)
            {
                if (document.ImagePath is null &&
                    field.FieldDefinition.Name == "MediaField" &&
                    content[part.Name]?[field.Name] is JsonObject media)
                {
                    document.ImagePath = FieldValueReader.GetStrings(media, "Paths").FirstOrDefault();
                }
            }
        }

        foreach (var field in _attributeCatalog.GetFields(definition))
        {
            var attributeSettings = field.Field.GetSettings<ProductAttributeSettings>();

            if (!attributeSettings.IsUsed)
            {
                continue;
            }

            var values = await field.Provider.GetValuesAsync(new ProductAttributeValueContext
            {
                ContentItem = contentItem,
                Field = field.Field,
                Value = content[field.Part.Name]?[field.Field.Name] as JsonObject,
                Key = field.Field.Name,
                Settings = attributeSettings,
            });

            document.Attributes.AddRange(values);
        }

        foreach (var contributor in _contributors)
        {
            await contributor.ContributeAsync(contentItem, document);
        }

        return document;
    }

    /// <summary>
    /// The culture of a localized product, read from its stored localization part so the Products module needs no
    /// reference to the Content Localization module.
    /// </summary>
    internal static string GetCulture(ContentItem contentItem)
    {
        var culture = ((JsonObject)contentItem.Content)["LocalizationPart"]?["Culture"];

        return culture is JsonValue value && value.TryGetValue<string>(out var name) && !string.IsNullOrWhiteSpace(name) ? name : null;
    }
}
