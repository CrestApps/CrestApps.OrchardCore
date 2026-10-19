using System.Globalization;
using System.Text.Json;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Transfer;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Products.ContentTransfer;

/// <summary>
/// Reads a Shopify or WooCommerce product export, where a product spans a row per variant, as one row per product with
/// the product type's import columns: title, description, prices, SKU, options and variants, the category and brand
/// by name, and stock. The content item id is left for <see cref="ProductImportRowFilter"/> to fill in from a product's
/// SKUs, so a product already in the store is updated.
/// </summary>
public sealed class StoreExportReaderTransformer : IContentTransferFileReaderTransformer
{
    internal readonly Microsoft.Extensions.Localization.IStringLocalizer S;

    public StoreExportReaderTransformer(Microsoft.Extensions.Localization.IStringLocalizer<StoreExportReaderTransformer> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public string Describe(ContentTypeDefinition contentType)
        => contentType is not null && PartName(contentType, nameof(ProductPart)) is not null
            ? S["A product export from Shopify (Products › Export) or WooCommerce (Products › Export) is read as it is: a product's variants come in with it, its category and brand are matched to their terms by name, and a product already here, matched by its SKU or a variant's, is updated. Images aren't downloaded."]
            : null;

    public Task<IContentTransferFileReader> TransformAsync(IContentTransferFileReader reader, ContentTypeDefinition contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var headers = reader.GetColumnNames();

        if (contentType is null || !ProductCsv.IsStoreExport(headers) || PartName(contentType, nameof(ProductPart)) is not { } productPart)
        {
            return Task.FromResult<IContentTransferFileReader>(null);
        }

        var read = ProductCsv.Read(headers, reader.ReadRows());
        var titlePart = PartName(contentType, "TitlePart");
        var bodyPart = PartName(contentType, "HtmlBodyPart");
        var variantsPart = contentType.Parts.FirstOrDefault(part => part.PartDefinition.Name == nameof(ProductVariantsPart));
        var inventoryPart = PartName(contentType, "InventoryPart");
        var offered = ProductVariantsPartContentImportHandler.OfferedOptions(variantsPart);
        var category = TaxonomyColumn(contentType, "categor", "type", "collection");
        var brand = TaxonomyColumn(contentType, "brand", "vendor", "manufacturer");

        var columns = new List<string> { nameof(ContentItem.ContentItemId) };

        if (titlePart is not null)
        {
            columns.Add(titlePart + "_Title");
        }

        if (bodyPart is not null)
        {
            columns.Add(bodyPart + "_Html");
        }

        columns.AddRange([productPart + "_Price", productPart + "_CompareAtPrice", productPart + "_Sku", productPart + "_ExternalUrl", productPart + "_ExternalButtonText"]);

        if (variantsPart is not null)
        {
            columns.AddRange([variantsPart.Name + "_Options", variantsPart.Name + "_Variants"]);
        }

        if (category is not null)
        {
            columns.Add(category);
        }

        if (brand is not null)
        {
            columns.Add(brand);
        }

        if (inventoryPart is not null)
        {
            columns.AddRange([inventoryPart + "_Stock", inventoryPart + "_VariantStock"]);
        }

        var rows = new List<string[]>();

        foreach (var product in read.Products)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);

            // Variants come in when the type offers every option the product has; otherwise it comes in as one
            // product, from its lowest price, with the stock of all its variants.
            var withVariants = variantsPart is not null && product.Options.Count > 0 &&
                product.Options.All(option => offered.Contains(option.Name, StringComparer.OrdinalIgnoreCase));

            if (!withVariants && product.Variants.Count > 0)
            {
                product.Price = product.Variants.Select(variant => variant.Price).Where(price => price.HasValue).DefaultIfEmpty(product.Price).Min();
                product.CompareAtPrice ??= product.Variants.Select(variant => variant.CompareAtPrice).FirstOrDefault(price => price.HasValue);
                product.Quantity ??= product.Variants.Any(variant => variant.Quantity.HasValue) ? product.Variants.Sum(variant => variant.Quantity ?? 0) : null;
            }

            if (titlePart is not null)
            {
                values[titlePart + "_Title"] = product.Title;
            }

            if (bodyPart is not null)
            {
                values[bodyPart + "_Html"] = product.DescriptionHtml;
            }

            values[productPart + "_Price"] = Money(product.Price);
            values[productPart + "_CompareAtPrice"] = Money(product.CompareAtPrice);
            values[productPart + "_Sku"] = product.Sku ?? (withVariants ? null : product.Variants.FirstOrDefault()?.Sku);
            values[productPart + "_ExternalUrl"] = product.ExternalUrl;
            values[productPart + "_ExternalButtonText"] = product.ExternalButtonText;

            if (withVariants)
            {
                values[variantsPart.Name + "_Options"] = ProductVariantsPartContentImportHandler.WriteOptions(product.Options);
                values[variantsPart.Name + "_Variants"] = JsonSerializer.Serialize(product.Variants.Select(variant => new ProductVariantsPartContentImportHandler.VariantRow
                {
                    Options = variant.Options,
                    Sku = variant.Sku,
                    Price = variant.Price,
                    CompareAtPrice = variant.CompareAtPrice,
                }), ProductVariantsPartContentImportHandler.JsonOptions);
            }

            if (category is not null)
            {
                values[category] = product.Category;
            }

            if (brand is not null)
            {
                values[brand] = product.Brand;
            }

            if (inventoryPart is not null)
            {
                if (withVariants)
                {
                    values[inventoryPart + "_VariantStock"] = string.Join("; ", product.Variants
                        .Where(variant => variant.Quantity.HasValue)
                        .Select(variant => $"{(string.IsNullOrEmpty(variant.Sku) ? string.Join(" / ", variant.Options.Values) : variant.Sku)}={variant.Quantity.Value.ToString(CultureInfo.InvariantCulture)}"));
                }
                else
                {
                    values[inventoryPart + "_Stock"] = product.Quantity?.ToString(CultureInfo.InvariantCulture);
                }
            }

            rows.Add([.. columns.Select(column => values.GetValueOrDefault(column))]);
        }

        return Task.FromResult<IContentTransferFileReader>(new InMemoryContentTransferFileReader(columns, rows));
    }

    private static string PartName(ContentTypeDefinition type, string partDefinitionName)
        => type.Parts.FirstOrDefault(part => part.PartDefinition.Name == partDefinitionName)?.Name;

    // The first taxonomy field whose name or display name says what it is, as its Terms column.
    private static string TaxonomyColumn(ContentTypeDefinition type, params string[] words)
    {
        foreach (var part in type.Parts)
        {
            foreach (var field in part.PartDefinition.Fields.Where(field => field.FieldDefinition.Name == "TaxonomyField"))
            {
                var displayName = field.DisplayName() ?? string.Empty;

                if (words.Any(word => field.Name.Contains(word, StringComparison.OrdinalIgnoreCase) || displayName.Contains(word, StringComparison.OrdinalIgnoreCase)))
                {
                    return $"{part.Name}_{field.Name}_Terms";
                }
            }
        }

        return null;
    }

    private static string Money(decimal? amount)
        => amount?.ToString("0.00", CultureInfo.InvariantCulture);
}
