using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Services.Attributes;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Builds the search record of a published product from its content item.
/// </summary>
public sealed class ProductSearchDocumentBuilder
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ProductAttributeCatalog _attributeCatalog;

    public ProductSearchDocumentBuilder(
        IContentDefinitionManager contentDefinitionManager,
        ProductAttributeCatalog attributeCatalog)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _attributeCatalog = attributeCatalog;
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

        // A price that is not shown must not reach the index either: a price filter or a price sort over it
        // would tell visitors what the merchant chose not to.
        var shownPrice = priceDisplay == ProductPriceDisplay.Show ? product.Price : null;

        var document = new ProductSearchDocument
        {
            ContentItemId = contentItem.ContentItemId,
            ContentItemVersionId = contentItem.ContentItemVersionId,
            ContentType = contentItem.ContentType,
            Title = contentItem.DisplayText,
            Sku = product.Sku,
            Price = shownPrice,
            CompareAtPrice = shownPrice.HasValue ? product.CompareAtPrice : null,
            Currency = string.IsNullOrEmpty(product.Currency) ? settings?.DefaultCurrency : product.Currency,
            PriceDisplay = priceDisplay,
            IsPurchasable = isPurchasable,
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

        return document;
    }
}
